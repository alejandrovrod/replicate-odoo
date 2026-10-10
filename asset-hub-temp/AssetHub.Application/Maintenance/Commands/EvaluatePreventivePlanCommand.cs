using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Application.Maintenance.Helpers;
using AssetHub.Domain.Assets;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;
using Cronos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Maintenance.Commands;

public class EvaluatePreventivePlanCommand : IRequest<EvaluatePreventivePlansResult>
{
    public Guid PlanId { get; set; }
}

public class EvaluatePreventivePlanCommandHandler : IRequestHandler<EvaluatePreventivePlanCommand, EvaluatePreventivePlansResult>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<EvaluatePreventivePlanCommandHandler> _logger;

    public EvaluatePreventivePlanCommandHandler(ITenantDbContext db, IMediator mediator, ILogger<EvaluatePreventivePlanCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<EvaluatePreventivePlansResult> Handle(EvaluatePreventivePlanCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Evaluating preventive plan {PlanId}", request.PlanId);

        try
        {
            var now = DateTime.UtcNow;
            var result = new EvaluatePreventivePlansResult();

            var plan = await _db.PreventivePlans
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == request.PlanId && !p.IsDeleted, cancellationToken);

            if (plan == null)
            {
                _logger.LogWarning("Preventive plan {PlanId} not found.", request.PlanId);
                throw new ArgumentException($"Plan preventivo '{request.PlanId}' no encontrado.");
            }

            if (!plan.IsActive)
            {
                _logger.LogInformation("Preventive plan {PlanId} is not active; skipping.", request.PlanId);
                return result;
            }

            var occurrence = EnsureUtc(plan.NextRunAt ?? now);

            if (plan.EndsAt.HasValue && occurrence > plan.EndsAt.Value)
            {
                _logger.LogInformation("Plan {PlanId} occurrence {Occurrence} is past EndsAt {EndsAt}; deactivating without generating items.",
                    plan.Id, occurrence, plan.EndsAt.Value);
                plan.IsActive = false;
                await _db.SaveChangesAsync(cancellationToken);
                return result;
            }

            _logger.LogDebug("Plan {PlanId} occurrence: {Occurrence}, cron: {CronExpression}, endsAt: {EndsAt}",
                plan.Id, occurrence, plan.CronExpression, plan.EndsAt);

            var assets = await ResolveTargetAssetsAsync(plan, cancellationToken);
            _logger.LogInformation("Plan {PlanId} resolved {AssetCount} target assets.", plan.Id, assets.Count);

            var planGeneratedItems = new List<GeneratedItemInfo>();

            foreach (var asset in assets)
            {
                if (await AlreadyExecutedAsync(plan.Id, asset.Id, occurrence, cancellationToken))
                {
                    _logger.LogDebug("Plan {PlanId} already executed for asset {AssetId} at occurrence {Occurrence}; skipping.",
                        plan.Id, asset.Id, occurrence);
                    continue;
                }

                var conditionResult = EvaluateConditions(plan.ConditionRuleJson, asset.State);
                if (!conditionResult.Passed)
                {
                    _logger.LogInformation("Plan {PlanId} skipped for asset {AssetId}: {Reason}",
                        plan.Id, asset.Id, conditionResult.Reason);
                    _db.PreventivePlanExecutionLogs.Add(new PreventivePlanExecutionLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = plan.TenantId,
                        PreventivePlanId = plan.Id,
                        ExecutedAt = now,
                        Occurrence = occurrence,
                        AssetId = asset.Id,
                        Status = PreventivePlanConstants.ExecutionStatusSkipped,
                        Message = conditionResult.Reason
                    });
                    result.SkippedAssets++;
                    continue;
                }

                var executionGuardResult = await PreventivePlanExecutionGuard.CanExecuteForAssetAsync(_db, asset, plan.TenantId, cancellationToken);
                if (!executionGuardResult.CanExecute)
                {
                    _logger.LogInformation("Plan {PlanId} skipped for asset {AssetId}: {Reason}",
                        plan.Id, asset.Id, executionGuardResult.Reason);
                    _db.PreventivePlanExecutionLogs.Add(new PreventivePlanExecutionLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = plan.TenantId,
                        PreventivePlanId = plan.Id,
                        ExecutedAt = now,
                        Occurrence = occurrence,
                        AssetId = asset.Id,
                        Status = PreventivePlanConstants.ExecutionStatusSkipped,
                        Message = executionGuardResult.Reason
                    });
                    result.SkippedAssets++;
                    continue;
                }

                try
                {
                    var generated = await GenerateWorkItemsAsync(plan, asset, occurrence, cancellationToken);

                    var primaryItem = generated.FirstOrDefault();
                    _db.PreventivePlanExecutionLogs.Add(new PreventivePlanExecutionLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = plan.TenantId,
                        PreventivePlanId = plan.Id,
                        ExecutedAt = now,
                        Occurrence = occurrence,
                        AssetId = asset.Id,
                        Status = PreventivePlanConstants.ExecutionStatusSuccess,
                        GeneratedEntityType = primaryItem?.EntityType,
                        GeneratedEntityId = primaryItem?.EntityId
                    });

                    foreach (var item in generated)
                    {
                        planGeneratedItems.Add(new GeneratedItemInfo
                        {
                            EntityType = item.EntityType,
                            EntityId = item.EntityId,
                            AssetId = asset.Id,
                            AssetName = asset.Name
                        });

                        if (item.EntityType == PreventivePlanConstants.GeneratedEntityTypeWorkTask)
                            result.GeneratedWorkTasks++;
                        else
                            result.GeneratedMaintenanceOrders++;
                    }

                    _logger.LogInformation("Plan {PlanId} generated {GeneratedCount} items for asset {AssetId}.",
                        plan.Id, generated.Count, asset.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Plan {PlanId} failed to generate work items for asset {AssetId}.", plan.Id, asset.Id);
                    _db.PreventivePlanExecutionLogs.Add(new PreventivePlanExecutionLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = plan.TenantId,
                        PreventivePlanId = plan.Id,
                        ExecutedAt = now,
                        Occurrence = occurrence,
                        AssetId = asset.Id,
                        Status = PreventivePlanConstants.ExecutionStatusFailed,
                        Message = ex.Message
                    });
                    result.FailedAssets++;
                }
            }

            plan.LastRunAt = occurrence;
            var baseTime = occurrence > now ? occurrence : now;
            plan.NextRunAt = ComputeNextOccurrence(plan.CronExpression, baseTime, plan.EndsAt);

            if (plan.NextRunAt == null && plan.EndsAt.HasValue)
            {
                _logger.LogInformation("Plan {PlanId} has no more occurrences after {EndsAt}; deactivating.", plan.Id, plan.EndsAt.Value);
                plan.IsActive = false;
            }

            result.ProcessedPlans = 1;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Plan {PlanId} evaluation completed. Result: {Result}", plan.Id, JsonSerializer.Serialize(result));

            if (planGeneratedItems.Count > 0)
            {
                await _mediator.Publish(new PreventivePlanExecutedEvent
                {
                    PlanId = plan.Id,
                    PlanName = plan.Name,
                    TenantId = plan.TenantId,
                    AssignedEmployeeId = plan.DefaultAssignedEmployeeId,
                    AssignedTeamId = plan.DefaultAssignedTeamId,
                    GeneratedItems = planGeneratedItems
                }, cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception evaluating preventive plan {PlanId}.", request.PlanId);
            throw;
        }
    }

    private async Task<List<Domain.Assets.Asset>> ResolveTargetAssetsAsync(PreventivePlan plan, CancellationToken cancellationToken)
    {
        if (plan.AssetId.HasValue)
        {
            var asset = await _db.Assets
                .Include(a => a.AssetTemplate)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == plan.TenantId && a.Id == plan.AssetId.Value && !a.IsDeleted, cancellationToken);

            return asset != null ? new List<Domain.Assets.Asset> { asset } : new List<Domain.Assets.Asset>();
        }

        if (plan.AssetTemplateId.HasValue)
        {
            return await _db.Assets
                .Include(a => a.AssetTemplate)
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == plan.TenantId && a.AssetTemplateId == plan.AssetTemplateId.Value && !a.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        return new List<Domain.Assets.Asset>();
    }

    private async Task<bool> AlreadyExecutedAsync(Guid planId, Guid assetId, DateTime occurrence, CancellationToken cancellationToken)
    {
        return await _db.PreventivePlanExecutionLogs
            .IgnoreQueryFilters()
            .AnyAsync(e => e.PreventivePlanId == planId && e.AssetId == assetId && e.Occurrence == occurrence, cancellationToken);
    }

    private static (bool Passed, string? Reason) EvaluateConditions(string? conditionRuleJson, string assetState)
    {
        if (string.IsNullOrWhiteSpace(conditionRuleJson))
        {
            return (true, null);
        }

        try
        {
            var rule = JsonSerializer.Deserialize<ConditionRule>(conditionRuleJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (rule == null) return (true, null);

            if (rule.AllowedStates != null && rule.AllowedStates.Count > 0 && !rule.AllowedStates.Contains(assetState))
            {
                return (false, $"Asset state '{assetState}' is not in the allowed states list.");
            }

            if (rule.ExcludedStates != null && rule.ExcludedStates.Count > 0 && rule.ExcludedStates.Contains(assetState))
            {
                return (false, $"Asset state '{assetState}' is excluded by plan conditions.");
            }

            return (true, null);
        }
        catch
        {
            return (true, null);
        }
    }

    private async Task<List<GeneratedItem>> GenerateWorkItemsAsync(
        PreventivePlan plan,
        Domain.Assets.Asset asset,
        DateTime occurrence,
        CancellationToken cancellationToken)
    {
        var generated = new List<GeneratedItem>();
        var dueAt = occurrence.AddDays(plan.DueDateOffsetDays);

        var (typeId, priorityId) = await PreventivePlanCatalogDefaults.EnsureDefaultCatalogsAsync(_db, plan.TenantId, cancellationToken);

        _logger.LogDebug("Plan {PlanId} using default catalog items: typeId={TypeId}, priorityId={PriorityId}.",
            plan.Id, typeId, priorityId);

        Guid? maintenanceOrderId = null;

        if (plan.GeneratedEntityType == PreventivePlanConstants.GeneratedEntityTypeMaintenanceOrder ||
            plan.GeneratedEntityType == PreventivePlanConstants.GeneratedEntityTypeBoth)
        {
            var order = new MaintenanceOrder
            {
                Id = Guid.NewGuid(),
                TenantId = plan.TenantId,
                Kind = MaintenanceOrderKinds.Preventive,
                State = MaintenanceOrderStates.Draft,
                Title = plan.Name,
                Description = plan.Description,
                AssetId = asset.Id,
                PreventivePlanId = plan.Id,
                AssignedEmployeeId = plan.AutoAssign ? plan.DefaultAssignedEmployeeId : null,
                ScheduledStart = dueAt,
                PropertiesJson = asset.PropertiesJson
            };

            _db.MaintenanceOrders.Add(order);
            generated.Add(new GeneratedItem(PreventivePlanConstants.GeneratedEntityTypeMaintenanceOrder, order.Id));
            maintenanceOrderId = order.Id;

            await _mediator.Publish(new AssetHub.Application.Maintenance.Events.MaintenanceOrderCreatedEvent(
                order.Id,
                order.TenantId,
                order.AssetId,
                order.PropertiesJson
            ), cancellationToken);
        }

        bool hasChecklistTasks = false;
        List<ChecklistTaskSchema> checklistTasks = new();

        if (!string.IsNullOrWhiteSpace(asset.AssetTemplate?.MaintenanceChecklist))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ChecklistSchema>(asset.AssetTemplate.MaintenanceChecklist, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed?.Tasks != null && parsed.Tasks.Count > 0)
                {
                    hasChecklistTasks = true;
                    checklistTasks = parsed.Tasks;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse MaintenanceChecklist for AssetTemplate {TemplateId}", asset.AssetTemplateId);
            }
        }

        if (hasChecklistTasks && maintenanceOrderId.HasValue)
        {
            foreach (var ct in checklistTasks)
            {
                var task = new WorkTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = plan.TenantId,
                    Title = ct.Title ?? "Tarea de checklist",
                    Description = !string.IsNullOrWhiteSpace(ct.Frequency) 
                        ? $"Frecuencia sugerida: {ct.Frequency}\n{ct.Description}" 
                        : ct.Description,
                    State = "todo",
                    TaskTypeCatalogItemId = typeId,
                    PriorityCatalogItemId = priorityId,
                    DueAt = dueAt,
                    AssetId = asset.Id,
                    MaintenanceOrderId = maintenanceOrderId,
                    PreventivePlanId = plan.Id,
                    AssignedEmployeeId = plan.AutoAssign ? plan.DefaultAssignedEmployeeId : null,
                    AssignedTeamId = plan.AutoAssign ? plan.DefaultAssignedTeamId : null,
                    IsIndependent = false,
                    PropertiesJson = asset.PropertiesJson
                };

                _db.WorkTasks.Add(task);
                generated.Add(new GeneratedItem(PreventivePlanConstants.GeneratedEntityTypeWorkTask, task.Id));

                await _mediator.Publish(new AssetHub.Application.Tasks.Events.WorkTaskCreatedEvent(
                    task.Id,
                    task.TenantId,
                    task.AssetId,
                    task.PropertiesJson
                ), cancellationToken);
            }
        }
        else if (plan.GeneratedEntityType == PreventivePlanConstants.GeneratedEntityTypeWorkTask ||
                 plan.GeneratedEntityType == PreventivePlanConstants.GeneratedEntityTypeBoth)
        {
            var task = new WorkTask
            {
                Id = Guid.NewGuid(),
                TenantId = plan.TenantId,
                Title = plan.Name,
                Description = plan.Description,
                State = "todo",
                TaskTypeCatalogItemId = typeId,
                PriorityCatalogItemId = priorityId,
                DueAt = dueAt,
                AssetId = asset.Id,
                MaintenanceOrderId = maintenanceOrderId,
                PreventivePlanId = plan.Id,
                AssignedEmployeeId = plan.AutoAssign ? plan.DefaultAssignedEmployeeId : null,
                AssignedTeamId = plan.AutoAssign ? plan.DefaultAssignedTeamId : null,
                IsIndependent = false,
                PropertiesJson = asset.PropertiesJson
            };

            _db.WorkTasks.Add(task);
            generated.Add(new GeneratedItem(PreventivePlanConstants.GeneratedEntityTypeWorkTask, task.Id));

            await _mediator.Publish(new AssetHub.Application.Tasks.Events.WorkTaskCreatedEvent(
                task.Id,
                task.TenantId,
                task.AssetId,
                task.PropertiesJson
            ), cancellationToken);
        }

        return generated;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime? ComputeNextOccurrence(string cronExpression, DateTime fromOccurrence, DateTime? endsAt)
    {
        var expression = CronExpression.Parse(cronExpression);
        var tz = AssetHub.Application.Common.Time.TimeHelper.GetMexicoCityTimeZone();
        var next = expression.GetNextOccurrence(EnsureUtc(fromOccurrence), tz);

        if (next == null) return null;
        if (endsAt.HasValue && next.Value > endsAt.Value) return null;

        return next.Value;
    }

    private class ConditionRule
    {
        public List<string>? AllowedStates { get; set; }
        public List<string>? ExcludedStates { get; set; }
    }

    private class ChecklistSchema
    {
        public List<ChecklistTaskSchema>? Tasks { get; set; }
    }
    
    private class ChecklistTaskSchema
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Frequency { get; set; }
    }

    private record GeneratedItem(string EntityType, Guid EntityId);
}
