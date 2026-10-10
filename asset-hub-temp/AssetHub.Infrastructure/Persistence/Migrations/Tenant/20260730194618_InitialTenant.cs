using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class InitialTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenant");

            migrationBuilder.CreateTable(
                name: "BusinessEntityTypes",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnabledModules = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultCatalogIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntityTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Catalogs",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskRecurrences",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IntervalDays = table.Column<int>(type: "int", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaskTemplateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskRecurrences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetTemplates",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessEntityTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowedChildTemplateIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LifecycleStates = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaintenanceChecklist = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTemplates_BusinessEntityTypes_BusinessEntityTypeId",
                        column: x => x.BusinessEntityTypeId,
                        principalSchema: "tenant",
                        principalTable: "BusinessEntityTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItems",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParentItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItems_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalSchema: "tenant",
                        principalTable: "Catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Geo = table.Column<Geometry>(type: "geography", nullable: true),
                    GeoType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstalledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommissionedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConditionIndex = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_AssetTemplates_AssetTemplateId",
                        column: x => x.AssetTemplateId,
                        principalSchema: "tenant",
                        principalTable: "AssetTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assets_Assets_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItemTranslations",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemTranslations_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoleCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_CatalogItems_RoleCatalogItemId",
                        column: x => x.RoleCatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetAttachments",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetAttachments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetAttributeValues",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ValueType = table.Column<int>(type: "int", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueNumber = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValueBool = table.Column<bool>(type: "bit", nullable: true),
                    ValueCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValueGeo = table.Column<Geometry>(type: "geography", nullable: true),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetAttributeValues_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetConditionHistories",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConditionIndex = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetConditionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetConditionHistories_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetHierarchies",
                schema: "tenant",
                columns: table => new
                {
                    AncestorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DescendantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetHierarchies", x => new { x.AncestorId, x.DescendantId });
                    table.ForeignKey(
                        name: "FK_AssetHierarchies_Assets_AncestorId",
                        column: x => x.AncestorId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetHierarchies_Assets_DescendantId",
                        column: x => x.DescendantId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetLifecycleEvents",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetLifecycleEvents_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Geo = table.Column<Geometry>(type: "geography", nullable: true),
                    GeoType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreventivePlans",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssetTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CronExpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IntervalDays = table.Column<int>(type: "int", nullable: true),
                    ConditionRuleJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventivePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreventivePlans_AssetTemplates_AssetTemplateId",
                        column: x => x.AssetTemplateId,
                        principalSchema: "tenant",
                        principalTable: "AssetTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PreventivePlans_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAvailabilities",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAvailabilities_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsLead = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "tenant",
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentAttachments",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentAttachments_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "tenant",
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceOrders",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreventivePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LaborCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceOrders_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceOrders_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "tenant",
                        principalTable: "Incidents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceOrders_PreventivePlans_PreventivePlanId",
                        column: x => x.PreventivePlanId,
                        principalSchema: "tenant",
                        principalTable: "PreventivePlans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceParts",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceParts_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceParts_MaintenanceOrders_MaintenanceOrderId",
                        column: x => x.MaintenanceOrderId,
                        principalSchema: "tenant",
                        principalTable: "MaintenanceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkTasks",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TaskTypeCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriorityCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsIndependent = table.Column<bool>(type: "bit", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaintenanceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskRecurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTasks_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkTasks_CatalogItems_PriorityCatalogItemId",
                        column: x => x.PriorityCatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkTasks_CatalogItems_TaskTypeCatalogItemId",
                        column: x => x.TaskTypeCatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkTasks_Employees_AssignedEmployeeId",
                        column: x => x.AssignedEmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkTasks_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "tenant",
                        principalTable: "Incidents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkTasks_MaintenanceOrders_MaintenanceOrderId",
                        column: x => x.MaintenanceOrderId,
                        principalSchema: "tenant",
                        principalTable: "MaintenanceOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkTasks_TaskRecurrences_TaskRecurrenceId",
                        column: x => x.TaskRecurrenceId,
                        principalSchema: "tenant",
                        principalTable: "TaskRecurrences",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkTasks_Teams_AssignedTeamId",
                        column: x => x.AssignedTeamId,
                        principalSchema: "tenant",
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaskComments",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskComments_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalSchema: "tenant",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskEvidences",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CapturedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskEvidences_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalSchema: "tenant",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskStatusHistories",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskStatusHistories_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalSchema: "tenant",
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetAttachments_AssetId",
                schema: "tenant",
                table: "AssetAttachments",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAttributeValues_AssetId",
                schema: "tenant",
                table: "AssetAttributeValues",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAttributeValues_TenantId_AssetId_AttributeKey",
                schema: "tenant",
                table: "AssetAttributeValues",
                columns: new[] { "TenantId", "AssetId", "AttributeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetAttributeValues_ValueType",
                schema: "tenant",
                table: "AssetAttributeValues",
                column: "ValueType");

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionHistories_AssetId",
                schema: "tenant",
                table: "AssetConditionHistories",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetConditionHistories_TenantId_AssetId",
                schema: "tenant",
                table: "AssetConditionHistories",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetHierarchies_DescendantId_Depth",
                schema: "tenant",
                table: "AssetHierarchies",
                columns: new[] { "DescendantId", "Depth" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetLifecycleEvents_AssetId",
                schema: "tenant",
                table: "AssetLifecycleEvents",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetTemplateId",
                schema: "tenant",
                table: "Assets",
                column: "AssetTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Code_TenantId",
                schema: "tenant",
                table: "Assets",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ParentId",
                schema: "tenant",
                table: "Assets",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_TenantId_ParentId",
                schema: "tenant",
                table: "Assets",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_TenantId_Path",
                schema: "tenant",
                table: "Assets",
                columns: new[] { "TenantId", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplates_BusinessEntityTypeId",
                schema: "tenant",
                table: "AssetTemplates",
                column: "BusinessEntityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "AssetTemplates",
                columns: new[] { "Code", "Version", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityTypes_Code_TenantId",
                schema: "tenant",
                table: "BusinessEntityTypes",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_CatalogId_Code_TenantId",
                schema: "tenant",
                table: "CatalogItems",
                columns: new[] { "CatalogId", "Code", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemTranslations_CatalogItemId_Locale",
                schema: "tenant",
                table: "CatalogItemTranslations",
                columns: new[] { "CatalogItemId", "Locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Catalogs_Code_TenantId",
                schema: "tenant",
                table: "Catalogs",
                columns: new[] { "Code", "TenantId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAvailabilities_EmployeeId",
                schema: "tenant",
                table: "EmployeeAvailabilities",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAvailabilities_TenantId_EmployeeId",
                schema: "tenant",
                table: "EmployeeAvailabilities",
                columns: new[] { "TenantId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_RoleCatalogItemId",
                schema: "tenant",
                table: "Employees",
                column: "RoleCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_UserId",
                schema: "tenant",
                table: "Employees",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAttachments_IncidentId",
                schema: "tenant",
                table: "IncidentAttachments",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentAttachments_TenantId_IncidentId",
                schema: "tenant",
                table: "IncidentAttachments",
                columns: new[] { "TenantId", "IncidentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_AssetId",
                schema: "tenant",
                table: "Incidents",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TenantId_AssetId",
                schema: "tenant",
                table: "Incidents",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TenantId_State",
                schema: "tenant",
                table: "Incidents",
                columns: new[] { "TenantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_AssetId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_IncidentId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_PreventivePlanId",
                schema: "tenant",
                table: "MaintenanceOrders",
                column: "PreventivePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_TenantId_AssetId",
                schema: "tenant",
                table: "MaintenanceOrders",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_TenantId_State",
                schema: "tenant",
                table: "MaintenanceOrders",
                columns: new[] { "TenantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceParts_CatalogItemId",
                schema: "tenant",
                table: "MaintenanceParts",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceParts_MaintenanceOrderId",
                schema: "tenant",
                table: "MaintenanceParts",
                column: "MaintenanceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceParts_TenantId_MaintenanceOrderId",
                schema: "tenant",
                table: "MaintenanceParts",
                columns: new[] { "TenantId", "MaintenanceOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlans_AssetId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlans_AssetTemplateId",
                schema: "tenant",
                table: "PreventivePlans",
                column: "AssetTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventivePlans_TenantId_NextRunAt",
                schema: "tenant",
                table: "PreventivePlans",
                columns: new[] { "TenantId", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TenantId_WorkTaskId",
                schema: "tenant",
                table: "TaskComments",
                columns: new[] { "TenantId", "WorkTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_WorkTaskId",
                schema: "tenant",
                table: "TaskComments",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvidences_TenantId_WorkTaskId",
                schema: "tenant",
                table: "TaskEvidences",
                columns: new[] { "TenantId", "WorkTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskEvidences_WorkTaskId",
                schema: "tenant",
                table: "TaskEvidences",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRecurrences_TenantId_IsActive_NextRunAt",
                schema: "tenant",
                table: "TaskRecurrences",
                columns: new[] { "TenantId", "IsActive", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusHistories_TenantId_WorkTaskId",
                schema: "tenant",
                table: "TaskStatusHistories",
                columns: new[] { "TenantId", "WorkTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusHistories_WorkTaskId",
                schema: "tenant",
                table: "TaskStatusHistories",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_EmployeeId",
                schema: "tenant",
                table: "TeamMembers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId",
                schema: "tenant",
                table: "TeamMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TenantId_EmployeeId",
                schema: "tenant",
                table: "TeamMembers",
                columns: new[] { "TenantId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TenantId_TeamId",
                schema: "tenant",
                table: "TeamMembers",
                columns: new[] { "TenantId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_AssetId",
                schema: "tenant",
                table: "WorkTasks",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_AssignedEmployeeId",
                schema: "tenant",
                table: "WorkTasks",
                column: "AssignedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_AssignedTeamId",
                schema: "tenant",
                table: "WorkTasks",
                column: "AssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_IncidentId",
                schema: "tenant",
                table: "WorkTasks",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_MaintenanceOrderId",
                schema: "tenant",
                table: "WorkTasks",
                column: "MaintenanceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_PriorityCatalogItemId",
                schema: "tenant",
                table: "WorkTasks",
                column: "PriorityCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TaskRecurrenceId",
                schema: "tenant",
                table: "WorkTasks",
                column: "TaskRecurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TaskTypeCatalogItemId",
                schema: "tenant",
                table: "WorkTasks",
                column: "TaskTypeCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_AssetId",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_AssignedEmployeeId",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "AssignedEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_TenantId_State",
                schema: "tenant",
                table: "WorkTasks",
                columns: new[] { "TenantId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetAttachments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetAttributeValues",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetConditionHistories",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetHierarchies",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetLifecycleEvents",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "CatalogItemTranslations",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "EmployeeAvailabilities",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "IncidentAttachments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "MaintenanceParts",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "TaskComments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "TaskEvidences",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "TaskStatusHistories",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "TeamMembers",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "WorkTasks",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "MaintenanceOrders",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "TaskRecurrences",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "CatalogItems",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Incidents",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PreventivePlans",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Catalogs",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Assets",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetTemplates",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "BusinessEntityTypes",
                schema: "tenant");
        }
    }
}
