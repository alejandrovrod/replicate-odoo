using System.Threading;
using System.Threading.Tasks;
using AssetHub.Domain.AssetTemplates;
using AssetHub.Domain.Catalogs;
using AssetHub.Domain.EntityTypes;
using AssetHub.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Interfaces;

public interface ITenantDbContext
{
    DbSet<Catalog> Catalogs { get; }
    DbSet<CatalogItem> CatalogItems { get; }
    DbSet<CatalogItemTranslation> CatalogItemTranslations { get; }
    DbSet<BusinessEntityType> BusinessEntityTypes { get; }
    DbSet<AssetTemplate> AssetTemplates { get; }
    DbSet<AssetHub.Domain.WorkflowTemplates.WorkflowTemplate> WorkflowTemplates { get; }

    DbSet<AssetHub.Domain.Assets.Asset> Assets { get; }
    DbSet<AssetHub.Domain.Assets.AssetConditionHistory> AssetConditionHistories { get; }
    DbSet<AssetHub.Domain.Assets.AssetHierarchy> AssetHierarchies { get; }
    DbSet<AssetHub.Domain.Assets.AssetAttachment> AssetAttachments { get; }
    DbSet<AssetHub.Domain.Assets.AssetLifecycleEvent> AssetLifecycleEvents { get; }
    DbSet<AssetHub.Domain.Assets.AssetAttributeValue> AssetAttributeValues { get; }
    DbSet<AssetHub.Domain.Assets.AssetHealthPrediction> AssetHealthPredictions { get; }
    DbSet<AssetHub.Domain.Assets.AssetMaterial> AssetMaterials { get; }
    
    DbSet<AssetHub.Domain.Incidents.Incident> Incidents { get; }
    DbSet<AssetHub.Domain.Incidents.IncidentAttachment> IncidentAttachments { get; }
    DbSet<AssetHub.Domain.Incidents.IncidentLifecycleEvent> IncidentLifecycleEvents { get; }
    DbSet<AssetHub.Domain.Maintenance.PreventivePlan> PreventivePlans { get; }
    DbSet<AssetHub.Domain.Maintenance.PreventivePlanExecutionLog> PreventivePlanExecutionLogs { get; }

    DbSet<AssetHub.Domain.Maintenance.MaintenanceOrder> MaintenanceOrders { get; }
    DbSet<AssetHub.Domain.Maintenance.MaintenancePart> MaintenanceParts { get; }
    DbSet<AssetHub.Domain.Analytics.CostEntry> CostEntries { get; }
    
    DbSet<AssetHub.Domain.Staff.Employee> Employees { get; }
    DbSet<AssetHub.Domain.Staff.EmployeeAvailability> EmployeeAvailabilities { get; }
    DbSet<AssetHub.Domain.Staff.Team> Teams { get; }
    DbSet<AssetHub.Domain.Staff.TeamMember> TeamMembers { get; }
    
    DbSet<AssetHub.Domain.Tasks.WorkTask> WorkTasks { get; }
    DbSet<AssetHub.Domain.Tasks.TaskRecurrence> TaskRecurrences { get; }
    DbSet<AssetHub.Domain.Tasks.TaskStatusHistory> TaskStatusHistories { get; }
    DbSet<AssetHub.Domain.Tasks.TaskEvidence> TaskEvidences { get; }
    DbSet<AssetHub.Domain.Tasks.TaskComment> TaskComments { get; }

    DbSet<AssetFinanceBook> AssetFinanceBooks { get; }
    DbSet<AssetDepreciationSchedule> AssetDepreciationSchedules { get; }
    DbSet<AssetDepreciationEntry> AssetDepreciationEntries { get; }
    DbSet<AssetValueAdjustment> AssetValueAdjustments { get; }
    DbSet<AssetDisposal> AssetDisposals { get; }
    DbSet<AssetCustodyTransfer> AssetCustodyTransfers { get; }
    DbSet<AssetRepairCapitalization> AssetRepairCapitalizations { get; }

    DbSet<AssetHub.Domain.Notifications.Notification> Notifications { get; }

    DbSet<AssetHub.Domain.Inventory.Warehouse> Warehouses { get; }
    DbSet<AssetHub.Domain.Inventory.StockBalance> StockBalances { get; }
    DbSet<AssetHub.Domain.Inventory.InventoryTransaction> InventoryTransactions { get; }
    DbSet<AssetHub.Domain.Inventory.TenantInventorySettings> TenantInventorySettings { get; }

    DbSet<AssetHub.Domain.CommunicationTemplates.CommunicationTemplate> CommunicationTemplates { get; }
    DbSet<AssetHub.Domain.CommunicationTemplates.CommunicationTemplateVersion> CommunicationTemplateVersions { get; }
    DbSet<AssetHub.Domain.CommunicationTemplates.CommunicationTemplateTranslation> CommunicationTemplateTranslations { get; }
    DbSet<AssetHub.Domain.CommunicationTemplates.NotificationMapping> NotificationMappings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
