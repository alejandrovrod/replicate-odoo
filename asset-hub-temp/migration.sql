IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF SCHEMA_ID(N'tenant') IS NULL EXEC(N'CREATE SCHEMA [tenant];');

CREATE TABLE [tenant].[BusinessEntityTypes] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Icon] nvarchar(max) NOT NULL,
    [EnabledModules] nvarchar(max) NOT NULL,
    [DefaultCatalogIds] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_BusinessEntityTypes] PRIMARY KEY ([Id])
);

CREATE TABLE [tenant].[Catalogs] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [Code] nvarchar(450) NOT NULL,
    [Label] nvarchar(max) NOT NULL,
    [IsSystem] bit NOT NULL,
    CONSTRAINT [PK_Catalogs] PRIMARY KEY ([Id])
);

CREATE TABLE [tenant].[TaskRecurrences] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CronExpression] nvarchar(max) NULL,
    [IntervalDays] int NULL,
    [NextRunAt] datetime2 NOT NULL,
    [EndsAt] datetime2 NULL,
    [TaskTemplateJson] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_TaskRecurrences] PRIMARY KEY ([Id])
);

CREATE TABLE [tenant].[Teams] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
);

CREATE TABLE [tenant].[AssetTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [BusinessEntityTypeId] uniqueidentifier NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [SchemaJson] nvarchar(max) NOT NULL,
    [AllowedChildTemplateIds] nvarchar(max) NOT NULL,
    [LifecycleStates] nvarchar(max) NOT NULL,
    [MaintenanceChecklist] nvarchar(max) NOT NULL,
    [Version] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_AssetTemplates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetTemplates_BusinessEntityTypes_BusinessEntityTypeId] FOREIGN KEY ([BusinessEntityTypeId]) REFERENCES [tenant].[BusinessEntityTypes] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [tenant].[CatalogItems] (
    [Id] uniqueidentifier NOT NULL,
    [CatalogId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [Code] nvarchar(450) NOT NULL,
    [ParentItemId] uniqueidentifier NULL,
    [Order] int NOT NULL,
    [MetadataJson] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_CatalogItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CatalogItems_Catalogs_CatalogId] FOREIGN KEY ([CatalogId]) REFERENCES [tenant].[Catalogs] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[Assets] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AssetTemplateId] uniqueidentifier NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [Path] nvarchar(450) NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [State] nvarchar(max) NOT NULL,
    [Geo] geography NULL,
    [GeoType] nvarchar(max) NULL,
    [InstalledAt] datetime2 NULL,
    [CommissionedAt] datetime2 NULL,
    [ConditionIndex] decimal(18,2) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    CONSTRAINT [PK_Assets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Assets_AssetTemplates_AssetTemplateId] FOREIGN KEY ([AssetTemplateId]) REFERENCES [tenant].[AssetTemplates] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Assets_Assets_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [tenant].[CatalogItemTranslations] (
    [Id] uniqueidentifier NOT NULL,
    [CatalogItemId] uniqueidentifier NOT NULL,
    [Locale] nvarchar(450) NOT NULL,
    [Label] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CatalogItemTranslations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CatalogItemTranslations_CatalogItems_CatalogItemId] FOREIGN KEY ([CatalogItemId]) REFERENCES [tenant].[CatalogItems] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[Employees] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [RoleCatalogItemId] uniqueidentifier NOT NULL,
    [SkillsJson] nvarchar(max) NOT NULL,
    [UserId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Employees_CatalogItems_RoleCatalogItemId] FOREIGN KEY ([RoleCatalogItemId]) REFERENCES [tenant].[CatalogItems] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[AssetAttachments] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [BlobUri] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AssetAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetAttachments_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[AssetAttributeValues] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [AttributeKey] nvarchar(450) NOT NULL,
    [ValueType] int NOT NULL,
    [ValueText] nvarchar(max) NULL,
    [ValueNumber] decimal(18,2) NULL,
    [ValueDate] datetime2 NULL,
    [ValueBool] bit NULL,
    [ValueCatalogItemId] uniqueidentifier NULL,
    [ValueGeo] geography NULL,
    [ValueJson] nvarchar(max) NULL,
    CONSTRAINT [PK_AssetAttributeValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetAttributeValues_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[AssetConditionHistories] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [ConditionIndex] decimal(18,2) NOT NULL,
    [CapturedAt] datetime2 NOT NULL,
    [Reason] nvarchar(max) NULL,
    CONSTRAINT [PK_AssetConditionHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetConditionHistories_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[AssetHierarchies] (
    [AncestorId] uniqueidentifier NOT NULL,
    [DescendantId] uniqueidentifier NOT NULL,
    [Depth] int NOT NULL,
    CONSTRAINT [PK_AssetHierarchies] PRIMARY KEY ([AncestorId], [DescendantId]),
    CONSTRAINT [FK_AssetHierarchies_Assets_AncestorId] FOREIGN KEY ([AncestorId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetHierarchies_Assets_DescendantId] FOREIGN KEY ([DescendantId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [tenant].[AssetLifecycleEvents] (
    [Id] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [EventType] nvarchar(max) NOT NULL,
    [FromState] nvarchar(max) NOT NULL,
    [ToState] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [At] datetime2 NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetLifecycleEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetLifecycleEvents_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[Incidents] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [TypeId] uniqueidentifier NOT NULL,
    [PriorityId] uniqueidentifier NULL,
    [State] nvarchar(450) NOT NULL,
    [Geo] geography NULL,
    [GeoType] nvarchar(max) NULL,
    [ReportedAt] datetime2 NOT NULL,
    [ResolvedAt] datetime2 NULL,
    [ClosedAt] datetime2 NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Incidents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Incidents_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[PreventivePlans] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [AssetTemplateId] uniqueidentifier NULL,
    [AssetId] uniqueidentifier NULL,
    [CronExpression] nvarchar(max) NULL,
    [IntervalDays] int NULL,
    [ConditionRuleJson] nvarchar(max) NULL,
    [NextRunAt] datetime2 NULL,
    [LastRunAt] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_PreventivePlans] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PreventivePlans_AssetTemplates_AssetTemplateId] FOREIGN KEY ([AssetTemplateId]) REFERENCES [tenant].[AssetTemplates] ([Id]),
    CONSTRAINT [FK_PreventivePlans_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id])
);

CREATE TABLE [tenant].[EmployeeAvailabilities] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [DayOfWeek] int NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [IsAvailable] bit NOT NULL,
    CONSTRAINT [PK_EmployeeAvailabilities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeAvailabilities_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [tenant].[Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[TeamMembers] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [IsLead] bit NOT NULL,
    CONSTRAINT [PK_TeamMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TeamMembers_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [tenant].[Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TeamMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [tenant].[Teams] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[IncidentAttachments] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [IncidentId] uniqueidentifier NOT NULL,
    [FileUrl] nvarchar(max) NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_IncidentAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentAttachments_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [tenant].[Incidents] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[MaintenanceOrders] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    [State] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [PreventivePlanId] uniqueidentifier NULL,
    [IncidentId] uniqueidentifier NULL,
    [AssignedEmployeeId] uniqueidentifier NULL,
    [ScheduledStart] datetime2 NULL,
    [ScheduledEnd] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [LaborCost] decimal(18,2) NOT NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_MaintenanceOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceOrders_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceOrders_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [tenant].[Incidents] ([Id]),
    CONSTRAINT [FK_MaintenanceOrders_PreventivePlans_PreventivePlanId] FOREIGN KEY ([PreventivePlanId]) REFERENCES [tenant].[PreventivePlans] ([Id])
);

CREATE TABLE [tenant].[MaintenanceParts] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [MaintenanceOrderId] uniqueidentifier NOT NULL,
    [CatalogItemId] uniqueidentifier NOT NULL,
    [Quantity] int NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_MaintenanceParts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaintenanceParts_CatalogItems_CatalogItemId] FOREIGN KEY ([CatalogItemId]) REFERENCES [tenant].[CatalogItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaintenanceParts_MaintenanceOrders_MaintenanceOrderId] FOREIGN KEY ([MaintenanceOrderId]) REFERENCES [tenant].[MaintenanceOrders] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[WorkTasks] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [State] nvarchar(450) NOT NULL,
    [TaskTypeCatalogItemId] uniqueidentifier NOT NULL,
    [PriorityCatalogItemId] uniqueidentifier NOT NULL,
    [DueAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [StartedAt] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [DueDate] datetime2 NULL,
    [IsIndependent] bit NOT NULL,
    [AssetId] uniqueidentifier NULL,
    [MaintenanceOrderId] uniqueidentifier NULL,
    [IncidentId] uniqueidentifier NULL,
    [AssignedEmployeeId] uniqueidentifier NULL,
    [AssignedTeamId] uniqueidentifier NULL,
    [TaskRecurrenceId] uniqueidentifier NULL,
    CONSTRAINT [PK_WorkTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkTasks_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]),
    CONSTRAINT [FK_WorkTasks_CatalogItems_PriorityCatalogItemId] FOREIGN KEY ([PriorityCatalogItemId]) REFERENCES [tenant].[CatalogItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkTasks_CatalogItems_TaskTypeCatalogItemId] FOREIGN KEY ([TaskTypeCatalogItemId]) REFERENCES [tenant].[CatalogItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkTasks_Employees_AssignedEmployeeId] FOREIGN KEY ([AssignedEmployeeId]) REFERENCES [tenant].[Employees] ([Id]),
    CONSTRAINT [FK_WorkTasks_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [tenant].[Incidents] ([Id]),
    CONSTRAINT [FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId] FOREIGN KEY ([MaintenanceOrderId]) REFERENCES [tenant].[MaintenanceOrders] ([Id]),
    CONSTRAINT [FK_WorkTasks_TaskRecurrences_TaskRecurrenceId] FOREIGN KEY ([TaskRecurrenceId]) REFERENCES [tenant].[TaskRecurrences] ([Id]),
    CONSTRAINT [FK_WorkTasks_Teams_AssignedTeamId] FOREIGN KEY ([AssignedTeamId]) REFERENCES [tenant].[Teams] ([Id])
);

CREATE TABLE [tenant].[TaskComments] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [WorkTaskId] uniqueidentifier NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [AuthorUserId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaskComments_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [tenant].[WorkTasks] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[TaskEvidences] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [WorkTaskId] uniqueidentifier NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [BlobUri] nvarchar(max) NULL,
    [Note] nvarchar(max) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [CapturedByUserId] uniqueidentifier NOT NULL,
    [CapturedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_TaskEvidences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaskEvidences_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [tenant].[WorkTasks] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [tenant].[TaskStatusHistories] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [WorkTaskId] uniqueidentifier NOT NULL,
    [FromState] nvarchar(max) NOT NULL,
    [ToState] nvarchar(max) NOT NULL,
    [ChangedByUserId] uniqueidentifier NULL,
    [ChangedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_TaskStatusHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaskStatusHistories_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [tenant].[WorkTasks] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AssetAttachments_AssetId] ON [tenant].[AssetAttachments] ([AssetId]);

CREATE INDEX [IX_AssetAttributeValues_AssetId] ON [tenant].[AssetAttributeValues] ([AssetId]);

CREATE INDEX [IX_AssetAttributeValues_TenantId_AssetId_AttributeKey] ON [tenant].[AssetAttributeValues] ([TenantId], [AssetId], [AttributeKey]);

CREATE INDEX [IX_AssetAttributeValues_ValueType] ON [tenant].[AssetAttributeValues] ([ValueType]);

CREATE INDEX [IX_AssetConditionHistories_AssetId] ON [tenant].[AssetConditionHistories] ([AssetId]);

CREATE INDEX [IX_AssetConditionHistories_TenantId_AssetId] ON [tenant].[AssetConditionHistories] ([TenantId], [AssetId]);

CREATE INDEX [IX_AssetHierarchies_DescendantId_Depth] ON [tenant].[AssetHierarchies] ([DescendantId], [Depth]);

CREATE INDEX [IX_AssetLifecycleEvents_AssetId] ON [tenant].[AssetLifecycleEvents] ([AssetId]);

CREATE INDEX [IX_Assets_AssetTemplateId] ON [tenant].[Assets] ([AssetTemplateId]);

CREATE UNIQUE INDEX [IX_Assets_Code_TenantId] ON [tenant].[Assets] ([Code], [TenantId]);

CREATE INDEX [IX_Assets_ParentId] ON [tenant].[Assets] ([ParentId]);

CREATE INDEX [IX_Assets_TenantId_ParentId] ON [tenant].[Assets] ([TenantId], [ParentId]);

CREATE INDEX [IX_Assets_TenantId_Path] ON [tenant].[Assets] ([TenantId], [Path]);

CREATE INDEX [IX_AssetTemplates_BusinessEntityTypeId] ON [tenant].[AssetTemplates] ([BusinessEntityTypeId]);

CREATE UNIQUE INDEX [IX_AssetTemplates_Code_Version_TenantId] ON [tenant].[AssetTemplates] ([Code], [Version], [TenantId]);

CREATE UNIQUE INDEX [IX_BusinessEntityTypes_Code_TenantId] ON [tenant].[BusinessEntityTypes] ([Code], [TenantId]);

CREATE UNIQUE INDEX [IX_CatalogItems_CatalogId_Code_TenantId] ON [tenant].[CatalogItems] ([CatalogId], [Code], [TenantId]) WHERE [TenantId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_CatalogItemTranslations_CatalogItemId_Locale] ON [tenant].[CatalogItemTranslations] ([CatalogItemId], [Locale]);

CREATE UNIQUE INDEX [IX_Catalogs_Code_TenantId] ON [tenant].[Catalogs] ([Code], [TenantId]) WHERE [TenantId] IS NOT NULL;

CREATE INDEX [IX_EmployeeAvailabilities_EmployeeId] ON [tenant].[EmployeeAvailabilities] ([EmployeeId]);

CREATE INDEX [IX_EmployeeAvailabilities_TenantId_EmployeeId] ON [tenant].[EmployeeAvailabilities] ([TenantId], [EmployeeId]);

CREATE INDEX [IX_Employees_RoleCatalogItemId] ON [tenant].[Employees] ([RoleCatalogItemId]);

CREATE UNIQUE INDEX [IX_Employees_TenantId_UserId] ON [tenant].[Employees] ([TenantId], [UserId]) WHERE "UserId" IS NOT NULL;

CREATE INDEX [IX_IncidentAttachments_IncidentId] ON [tenant].[IncidentAttachments] ([IncidentId]);

CREATE INDEX [IX_IncidentAttachments_TenantId_IncidentId] ON [tenant].[IncidentAttachments] ([TenantId], [IncidentId]);

CREATE INDEX [IX_Incidents_AssetId] ON [tenant].[Incidents] ([AssetId]);

CREATE INDEX [IX_Incidents_TenantId_AssetId] ON [tenant].[Incidents] ([TenantId], [AssetId]);

CREATE INDEX [IX_Incidents_TenantId_State] ON [tenant].[Incidents] ([TenantId], [State]);

CREATE INDEX [IX_MaintenanceOrders_AssetId] ON [tenant].[MaintenanceOrders] ([AssetId]);

CREATE INDEX [IX_MaintenanceOrders_IncidentId] ON [tenant].[MaintenanceOrders] ([IncidentId]);

CREATE INDEX [IX_MaintenanceOrders_PreventivePlanId] ON [tenant].[MaintenanceOrders] ([PreventivePlanId]);

CREATE INDEX [IX_MaintenanceOrders_TenantId_AssetId] ON [tenant].[MaintenanceOrders] ([TenantId], [AssetId]);

CREATE INDEX [IX_MaintenanceOrders_TenantId_State] ON [tenant].[MaintenanceOrders] ([TenantId], [State]);

CREATE INDEX [IX_MaintenanceParts_CatalogItemId] ON [tenant].[MaintenanceParts] ([CatalogItemId]);

CREATE INDEX [IX_MaintenanceParts_MaintenanceOrderId] ON [tenant].[MaintenanceParts] ([MaintenanceOrderId]);

CREATE INDEX [IX_MaintenanceParts_TenantId_MaintenanceOrderId] ON [tenant].[MaintenanceParts] ([TenantId], [MaintenanceOrderId]);

CREATE INDEX [IX_PreventivePlans_AssetId] ON [tenant].[PreventivePlans] ([AssetId]);

CREATE INDEX [IX_PreventivePlans_AssetTemplateId] ON [tenant].[PreventivePlans] ([AssetTemplateId]);

CREATE INDEX [IX_PreventivePlans_TenantId_NextRunAt] ON [tenant].[PreventivePlans] ([TenantId], [NextRunAt]);

CREATE INDEX [IX_TaskComments_TenantId_WorkTaskId] ON [tenant].[TaskComments] ([TenantId], [WorkTaskId]);

CREATE INDEX [IX_TaskComments_WorkTaskId] ON [tenant].[TaskComments] ([WorkTaskId]);

CREATE INDEX [IX_TaskEvidences_TenantId_WorkTaskId] ON [tenant].[TaskEvidences] ([TenantId], [WorkTaskId]);

CREATE INDEX [IX_TaskEvidences_WorkTaskId] ON [tenant].[TaskEvidences] ([WorkTaskId]);

CREATE INDEX [IX_TaskRecurrences_TenantId_IsActive_NextRunAt] ON [tenant].[TaskRecurrences] ([TenantId], [IsActive], [NextRunAt]);

CREATE INDEX [IX_TaskStatusHistories_TenantId_WorkTaskId] ON [tenant].[TaskStatusHistories] ([TenantId], [WorkTaskId]);

CREATE INDEX [IX_TaskStatusHistories_WorkTaskId] ON [tenant].[TaskStatusHistories] ([WorkTaskId]);

CREATE INDEX [IX_TeamMembers_EmployeeId] ON [tenant].[TeamMembers] ([EmployeeId]);

CREATE INDEX [IX_TeamMembers_TeamId] ON [tenant].[TeamMembers] ([TeamId]);

CREATE INDEX [IX_TeamMembers_TenantId_EmployeeId] ON [tenant].[TeamMembers] ([TenantId], [EmployeeId]);

CREATE INDEX [IX_TeamMembers_TenantId_TeamId] ON [tenant].[TeamMembers] ([TenantId], [TeamId]);

CREATE INDEX [IX_WorkTasks_AssetId] ON [tenant].[WorkTasks] ([AssetId]);

CREATE INDEX [IX_WorkTasks_AssignedEmployeeId] ON [tenant].[WorkTasks] ([AssignedEmployeeId]);

CREATE INDEX [IX_WorkTasks_AssignedTeamId] ON [tenant].[WorkTasks] ([AssignedTeamId]);

CREATE INDEX [IX_WorkTasks_IncidentId] ON [tenant].[WorkTasks] ([IncidentId]);

CREATE INDEX [IX_WorkTasks_MaintenanceOrderId] ON [tenant].[WorkTasks] ([MaintenanceOrderId]);

CREATE INDEX [IX_WorkTasks_PriorityCatalogItemId] ON [tenant].[WorkTasks] ([PriorityCatalogItemId]);

CREATE INDEX [IX_WorkTasks_TaskRecurrenceId] ON [tenant].[WorkTasks] ([TaskRecurrenceId]);

CREATE INDEX [IX_WorkTasks_TaskTypeCatalogItemId] ON [tenant].[WorkTasks] ([TaskTypeCatalogItemId]);

CREATE INDEX [IX_WorkTasks_TenantId_AssetId] ON [tenant].[WorkTasks] ([TenantId], [AssetId]);

CREATE INDEX [IX_WorkTasks_TenantId_AssignedEmployeeId] ON [tenant].[WorkTasks] ([TenantId], [AssignedEmployeeId]);

CREATE INDEX [IX_WorkTasks_TenantId_State] ON [tenant].[WorkTasks] ([TenantId], [State]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260730194618_InitialTenant', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260803234800_UpdateAssetTemplateJsonColumns', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [tenant].[Assets] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';

ALTER TABLE [tenant].[Assets] ADD [PropertiesJson] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [tenant].[Assets] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';

ALTER TABLE [tenant].[AssetAttachments] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';

ALTER TABLE [tenant].[AssetAttachments] ADD [TenantId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE [tenant].[AssetAttachments] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';

CREATE INDEX [IX_AssetAttachments_TenantId_AssetId] ON [tenant].[AssetAttachments] ([TenantId], [AssetId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260804024854_AddPropertiesJsonToAsset', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [tenant].[Catalogs] ADD [TargetModulesJson] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260813151605_AddCatalogTargetModules', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [tenant].[Incidents] ADD [IncidentTemplateId] uniqueidentifier NULL;

ALTER TABLE [tenant].[Incidents] ADD [PropertiesJson] nvarchar(max) NOT NULL DEFAULT N'';

CREATE TABLE [tenant].[IncidentTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [SchemaJson] nvarchar(max) NOT NULL,
    [LifecycleStates] nvarchar(max) NOT NULL,
    [Version] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_IncidentTemplates] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Incidents_IncidentTemplateId] ON [tenant].[Incidents] ([IncidentTemplateId]);

CREATE UNIQUE INDEX [IX_IncidentTemplates_Code_Version_TenantId] ON [tenant].[IncidentTemplates] ([Code], [Version], [TenantId]);

ALTER TABLE [tenant].[Incidents] ADD CONSTRAINT [FK_Incidents_IncidentTemplates_IncidentTemplateId] FOREIGN KEY ([IncidentTemplateId]) REFERENCES [tenant].[IncidentTemplates] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260813234031_AddIncidentTemplates', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [tenant].[IncidentLifecycleEvents] (
    [Id] uniqueidentifier NOT NULL,
    [IncidentId] uniqueidentifier NOT NULL,
    [EventType] nvarchar(max) NOT NULL,
    [FromState] nvarchar(max) NOT NULL,
    [ToState] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [PropertiesJson] nvarchar(max) NULL,
    [At] datetime2 NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_IncidentLifecycleEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidentLifecycleEvents_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [tenant].[Incidents] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_IncidentLifecycleEvents_IncidentId] ON [tenant].[IncidentLifecycleEvents] ([IncidentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260815070649_IncidentTimeline', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [tenant].[PreventivePlans] DROP CONSTRAINT [FK_PreventivePlans_AssetTemplates_AssetTemplateId];

ALTER TABLE [tenant].[PreventivePlans] DROP CONSTRAINT [FK_PreventivePlans_Assets_AssetId];

EXEC sp_rename N'[tenant].[PreventivePlans].[IntervalDays]', N'DueDateOffsetDays', 'COLUMN';

ALTER TABLE [tenant].[WorkTasks] ADD [PreventivePlanId] uniqueidentifier NULL;

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[tenant].[PreventivePlans]') AND [c].[name] = N'CronExpression');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [tenant].[PreventivePlans] DROP CONSTRAINT ' + @var + ';');
UPDATE [tenant].[PreventivePlans] SET [CronExpression] = N'' WHERE [CronExpression] IS NULL;
ALTER TABLE [tenant].[PreventivePlans] ALTER COLUMN [CronExpression] nvarchar(max) NOT NULL;
ALTER TABLE [tenant].[PreventivePlans] ADD DEFAULT N'' FOR [CronExpression];

ALTER TABLE [tenant].[PreventivePlans] ADD [AutoAssign] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [tenant].[PreventivePlans] ADD [DefaultAssignedEmployeeId] uniqueidentifier NULL;

ALTER TABLE [tenant].[PreventivePlans] ADD [DefaultAssignedTeamId] uniqueidentifier NULL;

ALTER TABLE [tenant].[PreventivePlans] ADD [EndsAt] datetime2 NULL;

ALTER TABLE [tenant].[PreventivePlans] ADD [GeneratedEntityType] nvarchar(max) NOT NULL DEFAULT N'';

CREATE TABLE [tenant].[Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [RelatedEntityType] nvarchar(max) NULL,
    [RelatedEntityId] uniqueidentifier NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
);

CREATE TABLE [tenant].[PreventivePlanExecutionLogs] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [PreventivePlanId] uniqueidentifier NOT NULL,
    [ExecutedAt] datetime2 NOT NULL,
    [Occurrence] datetime2 NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [GeneratedEntityType] nvarchar(max) NULL,
    [GeneratedEntityId] uniqueidentifier NULL,
    [Message] nvarchar(max) NULL,
    CONSTRAINT [PK_PreventivePlanExecutionLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PreventivePlanExecutionLogs_PreventivePlans_PreventivePlanId] FOREIGN KEY ([PreventivePlanId]) REFERENCES [tenant].[PreventivePlans] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_WorkTasks_PreventivePlanId] ON [tenant].[WorkTasks] ([PreventivePlanId]);

CREATE INDEX [IX_WorkTasks_TenantId_PreventivePlanId] ON [tenant].[WorkTasks] ([TenantId], [PreventivePlanId]);

CREATE INDEX [IX_Notifications_TenantId_CreatedAt] ON [tenant].[Notifications] ([TenantId], [CreatedAt]);

CREATE INDEX [IX_Notifications_TenantId_UserId_IsRead] ON [tenant].[Notifications] ([TenantId], [UserId], [IsRead]);

CREATE INDEX [IX_PreventivePlanExecutionLogs_PreventivePlanId] ON [tenant].[PreventivePlanExecutionLogs] ([PreventivePlanId]);

CREATE INDEX [IX_PreventivePlanExecutionLogs_TenantId_AssetId] ON [tenant].[PreventivePlanExecutionLogs] ([TenantId], [AssetId]);

CREATE UNIQUE INDEX [IX_PreventivePlanExecutionLogs_TenantId_PreventivePlanId_AssetId_Occurrence] ON [tenant].[PreventivePlanExecutionLogs] ([TenantId], [PreventivePlanId], [AssetId], [Occurrence]);

CREATE INDEX [IX_PreventivePlanExecutionLogs_TenantId_PreventivePlanId_Occurrence] ON [tenant].[PreventivePlanExecutionLogs] ([TenantId], [PreventivePlanId], [Occurrence]);

ALTER TABLE [tenant].[PreventivePlans] ADD CONSTRAINT [FK_PreventivePlans_AssetTemplates_AssetTemplateId] FOREIGN KEY ([AssetTemplateId]) REFERENCES [tenant].[AssetTemplates] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[PreventivePlans] ADD CONSTRAINT [FK_PreventivePlans_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[WorkTasks] ADD CONSTRAINT [FK_WorkTasks_PreventivePlans_PreventivePlanId] FOREIGN KEY ([PreventivePlanId]) REFERENCES [tenant].[PreventivePlans] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260816045635_AddPreventivePlanExecutionLogAndNotifications', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[tenant].[PreventivePlans]') AND [c].[name] = N'DueDateOffsetDays');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [tenant].[PreventivePlans] DROP CONSTRAINT ' + @var1 + ';');
UPDATE [tenant].[PreventivePlans] SET [DueDateOffsetDays] = 0 WHERE [DueDateOffsetDays] IS NULL;
ALTER TABLE [tenant].[PreventivePlans] ALTER COLUMN [DueDateOffsetDays] int NOT NULL;
ALTER TABLE [tenant].[PreventivePlans] ADD DEFAULT 0 FOR [DueDateOffsetDays];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260816051215_MakeDueDateOffsetDaysNotNull', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [tenant].[WorkTasks] DROP CONSTRAINT [FK_WorkTasks_Employees_AssignedEmployeeId];

ALTER TABLE [tenant].[WorkTasks] DROP CONSTRAINT [FK_WorkTasks_Incidents_IncidentId];

ALTER TABLE [tenant].[WorkTasks] DROP CONSTRAINT [FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId];

ALTER TABLE [tenant].[WorkTasks] DROP CONSTRAINT [FK_WorkTasks_TaskRecurrences_TaskRecurrenceId];

ALTER TABLE [tenant].[WorkTasks] DROP CONSTRAINT [FK_WorkTasks_Teams_AssignedTeamId];

DROP INDEX [IX_WorkTasks_TenantId_State] ON [tenant].[WorkTasks];

ALTER TABLE [tenant].[WorkTasks] ADD [DeletedAt] datetime2 NULL;

ALTER TABLE [tenant].[WorkTasks] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE INDEX [IX_WorkTasks_TenantId_AssignedTeamId] ON [tenant].[WorkTasks] ([TenantId], [AssignedTeamId]);

CREATE INDEX [IX_WorkTasks_TenantId_DueAt] ON [tenant].[WorkTasks] ([TenantId], [DueAt]);

CREATE INDEX [IX_WorkTasks_TenantId_IncidentId] ON [tenant].[WorkTasks] ([TenantId], [IncidentId]);

CREATE INDEX [IX_WorkTasks_TenantId_IsDeleted] ON [tenant].[WorkTasks] ([TenantId], [IsDeleted]);

CREATE INDEX [IX_WorkTasks_TenantId_MaintenanceOrderId] ON [tenant].[WorkTasks] ([TenantId], [MaintenanceOrderId]);

CREATE INDEX [IX_WorkTasks_TenantId_State_IsDeleted] ON [tenant].[WorkTasks] ([TenantId], [State], [IsDeleted]);

CREATE INDEX [IX_WorkTasks_TenantId_TaskRecurrenceId] ON [tenant].[WorkTasks] ([TenantId], [TaskRecurrenceId]);

ALTER TABLE [tenant].[WorkTasks] ADD CONSTRAINT [FK_WorkTasks_Employees_AssignedEmployeeId] FOREIGN KEY ([AssignedEmployeeId]) REFERENCES [tenant].[Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[WorkTasks] ADD CONSTRAINT [FK_WorkTasks_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [tenant].[Incidents] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[WorkTasks] ADD CONSTRAINT [FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId] FOREIGN KEY ([MaintenanceOrderId]) REFERENCES [tenant].[MaintenanceOrders] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[WorkTasks] ADD CONSTRAINT [FK_WorkTasks_TaskRecurrences_TaskRecurrenceId] FOREIGN KEY ([TaskRecurrenceId]) REFERENCES [tenant].[TaskRecurrences] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[WorkTasks] ADD CONSTRAINT [FK_WorkTasks_Teams_AssignedTeamId] FOREIGN KEY ([AssignedTeamId]) REFERENCES [tenant].[Teams] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260816201847_AddWorkTaskSoftDeleteAndTaskCommentsConfig', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE INDEX [IX_MaintenanceOrders_AssignedEmployeeId] ON [tenant].[MaintenanceOrders] ([AssignedEmployeeId]);

ALTER TABLE [tenant].[MaintenanceOrders] ADD CONSTRAINT [FK_MaintenanceOrders_Employees_AssignedEmployeeId] FOREIGN KEY ([AssignedEmployeeId]) REFERENCES [tenant].[Employees] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260817234157_AddMaintenanceOrderAssignedEmployeeNavigation', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
UPDATE tenant.Incidents SET PriorityId = NULL WHERE PriorityId IS NOT NULL AND PriorityId NOT IN (SELECT Id FROM tenant.CatalogItems)

DELETE FROM tenant.Incidents WHERE TypeId NOT IN (SELECT Id FROM tenant.CatalogItems)

ALTER TABLE [tenant].[Incidents] DROP CONSTRAINT [FK_Incidents_Assets_AssetId];

ALTER TABLE [tenant].[Incidents] DROP CONSTRAINT [FK_Incidents_IncidentTemplates_IncidentTemplateId];

ALTER TABLE [tenant].[MaintenanceOrders] DROP CONSTRAINT [FK_MaintenanceOrders_Assets_AssetId];

ALTER TABLE [tenant].[MaintenanceOrders] DROP CONSTRAINT [FK_MaintenanceOrders_Incidents_IncidentId];

ALTER TABLE [tenant].[MaintenanceOrders] DROP CONSTRAINT [FK_MaintenanceOrders_PreventivePlans_PreventivePlanId];

CREATE INDEX [IX_PreventivePlans_DefaultAssignedEmployeeId] ON [tenant].[PreventivePlans] ([DefaultAssignedEmployeeId]);

CREATE INDEX [IX_PreventivePlans_DefaultAssignedTeamId] ON [tenant].[PreventivePlans] ([DefaultAssignedTeamId]);

CREATE INDEX [IX_Incidents_PriorityId] ON [tenant].[Incidents] ([PriorityId]);

CREATE INDEX [IX_Incidents_TypeId] ON [tenant].[Incidents] ([TypeId]);

ALTER TABLE [tenant].[Incidents] ADD CONSTRAINT [FK_Incidents_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[Incidents] ADD CONSTRAINT [FK_Incidents_CatalogItems_PriorityId] FOREIGN KEY ([PriorityId]) REFERENCES [tenant].[CatalogItems] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[Incidents] ADD CONSTRAINT [FK_Incidents_CatalogItems_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [tenant].[CatalogItems] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[Incidents] ADD CONSTRAINT [FK_Incidents_IncidentTemplates_IncidentTemplateId] FOREIGN KEY ([IncidentTemplateId]) REFERENCES [tenant].[IncidentTemplates] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[MaintenanceOrders] ADD CONSTRAINT [FK_MaintenanceOrders_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [tenant].[Assets] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[MaintenanceOrders] ADD CONSTRAINT [FK_MaintenanceOrders_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [tenant].[Incidents] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[MaintenanceOrders] ADD CONSTRAINT [FK_MaintenanceOrders_PreventivePlans_PreventivePlanId] FOREIGN KEY ([PreventivePlanId]) REFERENCES [tenant].[PreventivePlans] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[PreventivePlans] ADD CONSTRAINT [FK_PreventivePlans_Employees_DefaultAssignedEmployeeId] FOREIGN KEY ([DefaultAssignedEmployeeId]) REFERENCES [tenant].[Employees] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [tenant].[PreventivePlans] ADD CONSTRAINT [FK_PreventivePlans_Teams_DefaultAssignedTeamId] FOREIGN KEY ([DefaultAssignedTeamId]) REFERENCES [tenant].[Teams] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260818202613_LinkMaintenanceWorkflows', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [tenant].[Incidents] DROP CONSTRAINT [FK_Incidents_IncidentTemplates_IncidentTemplateId];

EXEC sp_rename N'[tenant].[IncidentTemplates]', N'WorkflowTemplates', 'OBJECT';
DECLARE @defaultSchema2 nvarchar(max) = QUOTENAME(SCHEMA_NAME());
EXEC(N'ALTER SCHEMA ' + @defaultSchema2 + N' TRANSFER [tenant].[WorkflowTemplates];');

ALTER TABLE [tenant].[WorkflowTemplates] ADD [Type] nvarchar(max) NOT NULL DEFAULT N'incident';

EXEC sp_rename N'[tenant].[Incidents].[IncidentTemplateId]', N'WorkflowTemplateId', 'COLUMN';

EXEC sp_rename N'[tenant].[Incidents].[IX_Incidents_IncidentTemplateId]', N'IX_Incidents_WorkflowTemplateId', 'INDEX';

ALTER TABLE [tenant].[MaintenanceOrders] ADD [WorkflowTemplateId] uniqueidentifier NULL;

CREATE UNIQUE INDEX [IX_WorkflowTemplates_Code_Version_TenantId] ON [tenant].[WorkflowTemplates] ([Code], [Version], [TenantId]);

ALTER TABLE [tenant].[Incidents] ADD CONSTRAINT [FK_Incidents_WorkflowTemplates_WorkflowTemplateId] FOREIGN KEY ([WorkflowTemplateId]) REFERENCES [tenant].[WorkflowTemplates] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260819185656_RenameIncidentTemplatesToWorkflowTemplates', N'10.0.10');

COMMIT;
GO

