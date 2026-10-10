-- Migration: Predictive Maintenance with XGBoost
-- Target: SQL Server

BEGIN TRANSACTION;

IF SCHEMA_ID(N'tenant') IS NULL EXEC(N'CREATE SCHEMA [tenant];');

-- 1. Create AssetHealthPredictions Table
IF OBJECT_ID(N'[tenant].[AssetHealthPredictions]', N'U') IS NULL
BEGIN
    CREATE TABLE [tenant].[AssetHealthPredictions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AssetId] uniqueidentifier NOT NULL,
        [RiskProbability] decimal(5, 4) NOT NULL,
        [RiskLevel] nvarchar(50) NOT NULL,
        [PredictedFailureDays] int NULL,
        [TopFeatureContributionsJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_AssetHealthPredictions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssetHealthPredictions_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_AssetHealthPredictions_TenantId_AssetId] ON [tenant].[AssetHealthPredictions] ([TenantId], [AssetId]);
    CREATE INDEX [IX_AssetHealthPredictions_CreatedAt] ON [tenant].[AssetHealthPredictions] ([CreatedAt]);
END;

-- 2. Create Feature Extraction View for Machine Learning
IF OBJECT_ID(N'[tenant].[v_AssetMLFeatures]', N'V') IS NOT NULL
    DROP VIEW [tenant].[v_AssetMLFeatures];
GO

CREATE VIEW [tenant].[v_AssetMLFeatures] AS
SELECT 
    a.Id AS AssetId,
    a.TenantId,
    a.AssetTemplateId,
    a.Code AS AssetCode,
    a.Name AS AssetName,
    a.State AS AssetState,
    COALESCE(a.ConditionIndex, 100.0) AS CurrentConditionIndex,
    DATEDIFF(day, COALESCE(a.CommissionedAt, a.InstalledAt, a.CreatedAt), GETUTCDATE()) AS AssetAgeDays,
    
    -- Histórico de condición (promedio últimos 30 y 90 días)
    COALESCE((
        SELECT AVG(h.ConditionIndex)
        FROM [tenant].[AssetConditionHistories] h
        WHERE h.AssetId = a.Id AND h.CapturedAt >= DATEADD(day, -30, GETUTCDATE())
    ), a.ConditionIndex, 100.0) AS AvgConditionLast30Days,

    COALESCE((
        SELECT AVG(h.ConditionIndex)
        FROM [tenant].[AssetConditionHistories] h
        WHERE h.AssetId = a.Id AND h.CapturedAt >= DATEADD(day, -90, GETUTCDATE())
    ), a.ConditionIndex, 100.0) AS AvgConditionLast90Days,

    -- Incidencias previas
    (
        SELECT COUNT(1)
        FROM [tenant].[Incidents] i
        WHERE i.AssetId = a.Id AND i.IsDeleted = 0
    ) AS TotalIncidentsCount,

    (
        SELECT COUNT(1)
        FROM [tenant].[Incidents] i
        WHERE i.AssetId = a.Id AND i.IsDeleted = 0 AND i.ReportedAt >= DATEADD(day, -30, GETUTCDATE())
    ) AS IncidentsLast30Days,

    (
        SELECT COUNT(1)
        FROM [tenant].[Incidents] i
        WHERE i.AssetId = a.Id AND i.IsDeleted = 0 AND i.ReportedAt >= DATEADD(day, -90, GETUTCDATE())
    ) AS IncidentsLast90Days,

    -- Órdenes de Mantenimiento
    (
        SELECT COUNT(1)
        FROM [tenant].[MaintenanceOrders] m
        WHERE m.AssetId = a.Id AND m.IsDeleted = 0
    ) AS TotalMaintenanceOrdersCount,

    (
        SELECT COUNT(1)
        FROM [tenant].[MaintenanceOrders] m
        WHERE m.AssetId = a.Id AND m.IsDeleted = 0 AND m.State = 'done'
    ) AS CompletedMaintenanceOrdersCount,

    COALESCE(DATEDIFF(day, (
        SELECT MAX(m.CompletedAt)
        FROM [tenant].[MaintenanceOrders] m
        WHERE m.AssetId = a.Id AND m.IsDeleted = 0 AND m.State IN ('done', 'verified')
    ), GETUTCDATE()), DATEDIFF(day, COALESCE(a.CommissionedAt, a.InstalledAt, a.CreatedAt), GETUTCDATE())) AS DaysSinceLastCompletedMaintenance,

    -- Tareas de trabajo pendientes
    (
        SELECT COUNT(1)
        FROM [tenant].[WorkTasks] t
        WHERE t.AssetId = a.Id AND t.IsDeleted = 0 AND t.State NOT IN ('Done', 'Completed', 'Cancelled')
    ) AS PendingWorkTasksCount,

    -- === NUEVAS FEATURES DE ACTIVIDAD DE MANTENIMIENTO ===

    -- Órdenes creadas en los últimos 30 días (cualquier estado activo)
    (
        SELECT COUNT(1)
        FROM [tenant].[MaintenanceOrders] m
        WHERE m.AssetId = a.Id AND m.IsDeleted = 0
          AND m.CreatedAt >= DATEADD(day, -30, GETUTCDATE())
    ) AS MaintenanceOrdersLast30Days,

    -- WorkTasks asociadas a órdenes de los últimos 30 días
    (
        SELECT COUNT(1)
        FROM [tenant].[WorkTasks] t
        INNER JOIN [tenant].[MaintenanceOrders] m ON t.MaintenanceOrderId = m.Id
        WHERE m.AssetId = a.Id AND m.IsDeleted = 0 AND t.IsDeleted = 0
          AND m.CreatedAt >= DATEADD(day, -30, GETUTCDATE())
    ) AS MaintenanceTasksLast30Days,

    -- Órdenes vencidas: ScheduledEnd pasado y no en estado terminal
    (
        SELECT COUNT(1)
        FROM [tenant].[MaintenanceOrders] m
        WHERE m.AssetId = a.Id AND m.IsDeleted = 0
          AND m.ScheduledEnd < GETUTCDATE()
          AND m.State NOT IN ('done', 'verified', 'cancelled')
    ) AS OverdueOrdersCount

FROM [tenant].[Assets] a
WHERE a.IsDeleted = 0;
GO

COMMIT;
