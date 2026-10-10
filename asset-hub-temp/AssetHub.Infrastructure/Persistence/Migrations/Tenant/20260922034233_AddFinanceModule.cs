using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddFinanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetCustodyTransfers",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransferType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SignedByFrom = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignedByTo = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetCustodyTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetCustodyTransfers_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetCustodyTransfers_Assets_AssetId1",
                        column: x => x.AssetId1,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetCustodyTransfers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetCustodyTransfers_Employees_EmployeeId1",
                        column: x => x.EmployeeId1,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetCustodyTransfers_Employees_FromEmployeeId",
                        column: x => x.FromEmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetCustodyTransfers_Employees_ToEmployeeId",
                        column: x => x.ToEmployeeId,
                        principalSchema: "tenant",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetDisposals",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisposalType = table.Column<int>(type: "int", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NetBookValueAtDisposal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ProceedsAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GainLossAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDisposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Assets_AssetId1",
                        column: x => x.AssetId1,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetFinanceBooks",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ResidualValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "int", nullable: false),
                    DepreciationMethod = table.Column<int>(type: "int", nullable: false),
                    DepreciationRatePct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    FrequencyMonths = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetFinanceBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetFinanceBooks_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetFinanceBooks_Assets_AssetId1",
                        column: x => x.AssetId1,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetRepairCapitalizations",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapitalizedAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NewUsefulLifeMonths = table.Column<int>(type: "int", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaintenanceOrderId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetRepairCapitalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetRepairCapitalizations_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetRepairCapitalizations_Assets_AssetId1",
                        column: x => x.AssetId1,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetRepairCapitalizations_MaintenanceOrders_MaintenanceOrderId",
                        column: x => x.MaintenanceOrderId,
                        principalSchema: "tenant",
                        principalTable: "MaintenanceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetRepairCapitalizations_MaintenanceOrders_MaintenanceOrderId1",
                        column: x => x.MaintenanceOrderId1,
                        principalSchema: "tenant",
                        principalTable: "MaintenanceOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetValueAdjustments",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    PreviousNetBookValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AdjustmentAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NewNetBookValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetValueAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetValueAdjustments_AssetFinanceBooks_FinanceBookId",
                        column: x => x.FinanceBookId,
                        principalSchema: "tenant",
                        principalTable: "AssetFinanceBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetValueAdjustments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetValueAdjustments_Assets_AssetId1",
                        column: x => x.AssetId1,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetDepreciationEntries",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    AccountingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DepreciationAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetBookValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDepreciationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationEntries_AssetFinanceBooks_FinanceBookId",
                        column: x => x.FinanceBookId,
                        principalSchema: "tenant",
                        principalTable: "AssetFinanceBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationEntries_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationEntries_Assets_AssetId1",
                        column: x => x.AssetId1,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetDepreciationSchedules",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceBookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    PeriodStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectedDepreciationAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ProjectedAccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ProjectedNetBookValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    PostedEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssetId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDepreciationSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationSchedules_AssetDepreciationEntries_PostedEntryId",
                        column: x => x.PostedEntryId,
                        principalSchema: "tenant",
                        principalTable: "AssetDepreciationEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationSchedules_AssetFinanceBooks_FinanceBookId",
                        column: x => x.FinanceBookId,
                        principalSchema: "tenant",
                        principalTable: "AssetFinanceBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationSchedules_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDepreciationSchedules_Assets_AssetId1",
                        column: x => x.AssetId1,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetCustodyTransfers_AssetId",
                schema: "tenant",
                table: "AssetCustodyTransfers",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCustodyTransfers_AssetId1",
                schema: "tenant",
                table: "AssetCustodyTransfers",
                column: "AssetId1");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCustodyTransfers_EmployeeId",
                schema: "tenant",
                table: "AssetCustodyTransfers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCustodyTransfers_EmployeeId1",
                schema: "tenant",
                table: "AssetCustodyTransfers",
                column: "EmployeeId1");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCustodyTransfers_FromEmployeeId",
                schema: "tenant",
                table: "AssetCustodyTransfers",
                column: "FromEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCustodyTransfers_TenantId_AssetId_TransferDate",
                schema: "tenant",
                table: "AssetCustodyTransfers",
                columns: new[] { "TenantId", "AssetId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetCustodyTransfers_ToEmployeeId",
                schema: "tenant",
                table: "AssetCustodyTransfers",
                column: "ToEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationEntries_AssetId",
                schema: "tenant",
                table: "AssetDepreciationEntries",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationEntries_AssetId1",
                schema: "tenant",
                table: "AssetDepreciationEntries",
                column: "AssetId1");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationEntries_FinanceBookId",
                schema: "tenant",
                table: "AssetDepreciationEntries",
                column: "FinanceBookId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationEntries_ScheduleId",
                schema: "tenant",
                table: "AssetDepreciationEntries",
                column: "ScheduleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationEntries_TenantId_AssetId_PeriodNumber",
                schema: "tenant",
                table: "AssetDepreciationEntries",
                columns: new[] { "TenantId", "AssetId", "PeriodNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationEntries_TenantId_IdempotencyKey",
                schema: "tenant",
                table: "AssetDepreciationEntries",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_AssetId",
                schema: "tenant",
                table: "AssetDepreciationSchedules",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_AssetId1",
                schema: "tenant",
                table: "AssetDepreciationSchedules",
                column: "AssetId1");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_FinanceBookId",
                schema: "tenant",
                table: "AssetDepreciationSchedules",
                column: "FinanceBookId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_PostedEntryId",
                schema: "tenant",
                table: "AssetDepreciationSchedules",
                column: "PostedEntryId",
                unique: true,
                filter: "[PostedEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_AssetId_IsPosted",
                schema: "tenant",
                table: "AssetDepreciationSchedules",
                columns: new[] { "TenantId", "AssetId", "IsPosted" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciationSchedules_TenantId_AssetId_PeriodNumber",
                schema: "tenant",
                table: "AssetDepreciationSchedules",
                columns: new[] { "TenantId", "AssetId", "PeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_AssetId",
                schema: "tenant",
                table: "AssetDisposals",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_AssetId1",
                schema: "tenant",
                table: "AssetDisposals",
                column: "AssetId1");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_TenantId_AssetId",
                schema: "tenant",
                table: "AssetDisposals",
                columns: new[] { "TenantId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetFinanceBooks_AssetId",
                schema: "tenant",
                table: "AssetFinanceBooks",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetFinanceBooks_AssetId1",
                schema: "tenant",
                table: "AssetFinanceBooks",
                column: "AssetId1",
                unique: true,
                filter: "[AssetId1] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetFinanceBooks_TenantId_AssetId",
                schema: "tenant",
                table: "AssetFinanceBooks",
                columns: new[] { "TenantId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetRepairCapitalizations_AssetId",
                schema: "tenant",
                table: "AssetRepairCapitalizations",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetRepairCapitalizations_AssetId1",
                schema: "tenant",
                table: "AssetRepairCapitalizations",
                column: "AssetId1");

            migrationBuilder.CreateIndex(
                name: "IX_AssetRepairCapitalizations_MaintenanceOrderId",
                schema: "tenant",
                table: "AssetRepairCapitalizations",
                column: "MaintenanceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetRepairCapitalizations_MaintenanceOrderId1",
                schema: "tenant",
                table: "AssetRepairCapitalizations",
                column: "MaintenanceOrderId1",
                unique: true,
                filter: "[MaintenanceOrderId1] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssetRepairCapitalizations_TenantId_MaintenanceOrderId",
                schema: "tenant",
                table: "AssetRepairCapitalizations",
                columns: new[] { "TenantId", "MaintenanceOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetValueAdjustments_AssetId",
                schema: "tenant",
                table: "AssetValueAdjustments",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetValueAdjustments_AssetId1",
                schema: "tenant",
                table: "AssetValueAdjustments",
                column: "AssetId1");

            migrationBuilder.CreateIndex(
                name: "IX_AssetValueAdjustments_FinanceBookId",
                schema: "tenant",
                table: "AssetValueAdjustments",
                column: "FinanceBookId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetValueAdjustments_TenantId_AssetId_EffectiveDate",
                schema: "tenant",
                table: "AssetValueAdjustments",
                columns: new[] { "TenantId", "AssetId", "EffectiveDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDepreciationEntries_AssetDepreciationSchedules_ScheduleId",
                schema: "tenant",
                table: "AssetDepreciationEntries",
                column: "ScheduleId",
                principalSchema: "tenant",
                principalTable: "AssetDepreciationSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetDepreciationEntries_AssetDepreciationSchedules_ScheduleId",
                schema: "tenant",
                table: "AssetDepreciationEntries");

            migrationBuilder.DropTable(
                name: "AssetCustodyTransfers",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetDisposals",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetRepairCapitalizations",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetValueAdjustments",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetDepreciationSchedules",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetDepreciationEntries",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssetFinanceBooks",
                schema: "tenant");
        }
    }
}
