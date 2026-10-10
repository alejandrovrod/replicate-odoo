using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Application.Maintenance.Events;
using AssetHub.Application.Maintenance.Helpers;
using AssetHub.Domain.Catalogs;
using AssetHub.Domain.Maintenance;
using AssetHub.Domain.Tasks;
using Cronos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetHub.Application.Maintenance.Commands;

public class EvaluatePreventivePlansCommand : IRequest<EvaluatePreventivePlansResult>
{
}

public class EvaluatePreventivePlansResult
{
    public int ProcessedPlans { get; set; }
    public int GeneratedWorkTasks { get; set; }
    public int GeneratedMaintenanceOrders { get; set; }
    public int SkippedAssets { get; set; }
    public int FailedAssets { get; set; }
}

public class EvaluatePreventivePlansCommandHandler : IRequestHandler<EvaluatePreventivePlansCommand, EvaluatePreventivePlansResult>
{
    private readonly ITenantDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<EvaluatePreventivePlansCommandHandler> _logger;

    public EvaluatePreventivePlansCommandHandler(ITenantDbContext db, IMediator mediator, ILogger<EvaluatePreventivePlansCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<EvaluatePreventivePlansResult> Handle(EvaluatePreventivePlansCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting evaluation of all due preventive plans.");

        try
        {
            var now = DateTime.UtcNow;
            var result = new EvaluatePreventivePlansResult();

            var duePlans = await _db.PreventivePlans
                .IgnoreQueryFilters()
                .Where(p => p.IsActive && !p.IsDeleted && p.NextRunAt != null && p.NextRunAt <= now)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {DuePlanCount} preventive plans due for evaluation.", duePlans.Count);

            foreach (var plan in duePlans)
            {
                var occurrence = EnsureUtc(plan.NextRunAt!.Value);

                if (plan.EndsAt.HasValue && occurrence > plan.EndsAt.Value)
                {
                    _logger.LogInformation("Plan {PlanId} occurrence {Occurrence} is past EndsAt {EndsAt}; deactivating.",
                        plan.Id, occurrence, plan.EndsAt.Value);
                    plan.IsActive = false;
                    continue;
                }

                var assets = await ResolveTargetAssetsAsync(plan, cancellationToken);
                var planGeneratedItems = new List<GeneratedItemInfo>();

                _logger.LogInformation("Evaluating plan {PlanId} for {AssetCount} assets at occurrence {Occurrence}.",
                    plan.Id, assets.Count, occurrence);

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

                result.ProcessedPlans++;

                // Publish event for notifications
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
            }

            if (result.ProcessedPlans > 0 || result.SkippedAssets > 0 || result.FailedAssets > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Evaluation of all due preventive plans completed. Result: {Result}", JsonSerializer.Serialize(result));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception evaluating all due preventive plans.");
            throw;
        }
    }

    private async Task<List<AssetHub.Domain.Assets.Asset>> ResolveTargetAssetsAsync(PreventivePlan plan, CancellationToken cancellationToken)
    {
        if (plan.AssetId.HasValue)
        {
            var asset = await _db.Assets
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == plan.TenantId && a.Id == plan.AssetId.Value && !a.IsDeleted, cancellationToken);

            return asset != null ? new List<AssetHub.Domain.Assets.Asset> { asset } : new List<AssetHub.Domain.Assets.Asset>();
        }

        if (plan.AssetTemplateId.HasValue)
        {
            return await _db.Assets
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == plan.TenantId && a.AssetTemplateId == plan.AssetTemplateId.Value && !a.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        return new List<AssetHub.Domain.Assets.Asset>();
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
        AssetHub.Domain.Assets.Asset asset,
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
                Kind = "preventive",
                State = "draft",
                Title = plan.Name,
                Description = plan.Description,
                AssetId = asset.Id,
                PreventivePlanId = plan.Id,
                AssignedEmployeeId = plan.AutoAssign ? plan.DefaultAssignedEmployeeId : null,
                ScheduledStart = dueAt
            };

            _db.MaintenanceOrders.Add(order);
            generated.Add(new GeneratedItem(PreventivePlanConstants.GeneratedEntityTypeMaintenanceOrder, order.Id));
            maintenanceOrderId = order.Id;
        }

        if (plan.GeneratedEntityType == PreventivePlanConstants.GeneratedEntityTypeWorkTask ||
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
                IsIndependent = false
            };

            _db.WorkTasks.Add(task);
            generated.Add(new GeneratedItem(PreventivePlanConstants.GeneratedEntityTypeWorkTask, task.Id));
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

    private record GeneratedItem(string EntityType, Guid EntityId);
}
