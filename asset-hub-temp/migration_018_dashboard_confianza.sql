-- Migration: 018-dashboard-confiabilidad (AddCostEntriesAndReliabilityDates)
-- Idempotent script for CI/CD. Mirrors EF migration 20260911004631_AddCostEntriesAndReliabilityDates.
-- Target schema: tenant

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[tenant].[MaintenanceOrders]') AND name = N'FailureOccurredAt')
BEGIN
    ALTER TABLE [tenant].[MaintenanceOrders] ADD [FailureOccurredAt] datetime2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[tenant].[MaintenanceOrders]') AND name = N'RepairStartedAt')
BEGIN
    ALTER TABLE [tenant].[MaintenanceOrders] ADD [RepairStartedAt] datetime2 NULL;
END
GO

IF OBJECT_ID(N'[tenant].[CostEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [tenant].[CostEntries] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AssetId] uniqueidentifier NULL,
        [IncidentId] uniqueidentifier NULL,
        [WorkOrderId] uniqueidentifier NULL,
        [CostType] nvarchar(50) NOT NULL,
        [Amount] decimal(18, 4) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [IsEstimated] bit NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CostEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CostEntries_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CostEntries_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [tenant].[Incidents]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CostEntries_MaintenanceOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [tenant].[MaintenanceOrders]([Id]) ON DELETE NO ACTION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CostEntries_AssetId' AND object_id = OBJECT_ID(N'[tenant].[CostEntries]'))
BEGIN
    CREATE INDEX [IX_CostEntries_AssetId] ON [tenant].[CostEntries] ([AssetId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CostEntries_IncidentId' AND object_id = OBJECT_ID(N'[tenant].[CostEntries]'))
BEGIN
    CREATE INDEX [IX_CostEntries_IncidentId] ON [tenant].[CostEntries] ([IncidentId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CostEntries_TenantId_AssetId_OccurredAt' AND object_id = OBJECT_ID(N'[tenant].[CostEntries]'))
BEGIN
    CREATE INDEX [IX_CostEntries_TenantId_AssetId_OccurredAt] ON [tenant].[CostEntries] ([TenantId], [AssetId], [OccurredAt]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CostEntries_TenantId_IncidentId' AND object_id = OBJECT_ID(N'[tenant].[CostEntries]'))
BEGIN
    CREATE INDEX [IX_CostEntries_TenantId_IncidentId] ON [tenant].[CostEntries] ([TenantId], [IncidentId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CostEntries_TenantId_WorkOrderId' AND object_id = OBJECT_ID(N'[tenant].[CostEntries]'))
BEGIN
    CREATE INDEX [IX_CostEntries_TenantId_WorkOrderId] ON [tenant].[CostEntries] ([TenantId], [WorkOrderId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CostEntries_WorkOrderId' AND object_id = OBJECT_ID(N'[tenant].[CostEntries]'))
BEGIN
    CREATE INDEX [IX_CostEntries_WorkOrderId] ON [tenant].[CostEntries] ([WorkOrderId]);
END
GO
