using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.CommunicationTemplates.Rendering;

/// <summary>
/// Construye el diccionario de variables para renderizar plantillas a partir
/// de las entidades de dominio (orden, tarea, activo, destinatario).
/// Las entidades relacionadas se cargan por separado (sin Include) para evitar
/// que navegaciones inexistentes descarten la entidad raiz.
/// </summary>
public class TemplateVariableBuilder
{
    private readonly ITenantDbContext _db;

    public TemplateVariableBuilder(ITenantDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<string, object>> ForMaintenanceOrderAsync(
        Guid orderId, Guid recipientEmployeeId, CancellationToken ct = default)
    {
        var order = await _db.MaintenanceOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        var recipient = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == recipientEmployeeId, ct);

        var asset = order != null && order.AssetId != Guid.Empty
            ? await _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == order.AssetId, ct)
            : null;

        return new Dictionary<string, object>
        {
            ["order.id"] = order?.Id.ToString() ?? "",
            ["order.title"] = order?.Title ?? "",
            ["order.kind"] = order?.Kind ?? "",
            ["order.state"] = order?.State ?? "",
            ["order.scheduledStart"] = order?.ScheduledStart?.ToString("yyyy-MM-dd HH:mm") ?? "",
            ["order.scheduledEnd"] = order?.ScheduledEnd?.ToString("yyyy-MM-dd HH:mm") ?? "",
            ["asset.name"] = asset?.Name ?? "",
            ["asset.code"] = asset?.Code ?? "",
            ["recipient.name"] = RecipientName(recipient)
        };
    }

    public async Task<IReadOnlyDictionary<string, object>> ForWorkTaskAsync(
        Guid taskId, Guid recipientEmployeeId, CancellationToken ct = default)
    {
        var task = await _db.WorkTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

        var recipient = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == recipientEmployeeId, ct);

        var asset = task != null && task.AssetId.HasValue
            ? await _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == task.AssetId, ct)
            : null;

        var taskType = task != null && task.TaskTypeCatalogItemId != Guid.Empty
            ? await _db.CatalogItems.AsNoTracking().FirstOrDefaultAsync(ci => ci.Id == task.TaskTypeCatalogItemId, ct)
            : null;

        var priority = task != null && task.PriorityCatalogItemId != Guid.Empty
            ? await _db.CatalogItems.AsNoTracking().FirstOrDefaultAsync(ci => ci.Id == task.PriorityCatalogItemId, ct)
            : null;

        return new Dictionary<string, object>
        {
            ["task.id"] = task?.Id.ToString() ?? "",
            ["task.title"] = task?.Title ?? "",
            ["task.state"] = task?.State ?? "",
            ["task.type"] = taskType?.Code ?? "",
            ["task.priority"] = priority?.Code ?? "",
            ["task.dueAt"] = task?.DueAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
            ["asset.name"] = asset?.Name ?? "",
            ["asset.code"] = asset?.Code ?? "",
            ["recipient.name"] = RecipientName(recipient)
        };
    }

    public async Task<IReadOnlyDictionary<string, object>> ForAssetAsync(
        Guid assetId, string toState, Guid recipientEmployeeId, CancellationToken ct = default)
    {
        var asset = await _db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assetId, ct);

        var recipient = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == recipientEmployeeId, ct);

        return new Dictionary<string, object>
        {
            ["asset.name"] = asset?.Name ?? "",
            ["asset.code"] = asset?.Code ?? "",
            ["asset.state"] = toState ?? "",
            ["asset.toState"] = toState ?? "",
            ["recipient.name"] = RecipientName(recipient)
        };
    }

    private static string RecipientName(AssetHub.Domain.Staff.Employee? recipient) =>
        recipient == null ? "" : $"{recipient.FirstName} {recipient.LastName}".Trim();
}
