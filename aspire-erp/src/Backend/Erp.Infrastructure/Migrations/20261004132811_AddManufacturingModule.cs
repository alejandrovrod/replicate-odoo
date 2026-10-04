using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillOfMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 1m),
                    UomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RawMaterialCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    OperatingCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    ScrapCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    TotalCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillOfMaterials", x => x.Id);
                    table.CheckConstraint("CK_BOM_Costs", "[RawMaterialCost] >= 0.0000 AND [OperatingCost] >= 0.0000 AND [TotalCost] >= 0.0000");
                    table.CheckConstraint("CK_BOM_Quantity", "[Quantity] > 0.0000");
                    table.ForeignKey(
                        name: "FK_BOM_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BOM_UOM",
                        column: x => x.UomId,
                        principalTable: "UOM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BillOfMaterialsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Workstation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkstationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HourRateLabor = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    HourRateElectricity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    HourRateRent = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    HourRateTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, computedColumnSql: "[HourRateLabor] + [HourRateElectricity] + [HourRateRent]", stored: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workstation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workstation_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BomItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    BomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuationRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    ScrapPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomItem", x => x.Id);
                    table.CheckConstraint("CK_BOMItem_Amount", "[Amount] >= 0.0000");
                    table.CheckConstraint("CK_BOMItem_Quantity", "[Quantity] > 0.0000");
                    table.CheckConstraint("CK_BOMItem_Rate", "[ValuationRate] >= 0.0000");
                    table.ForeignKey(
                        name: "FK_BOMItem_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BomItem_BillOfMaterials_BomId",
                        column: x => x.BomId,
                        principalTable: "BillOfMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BomItem_UOM_UomId",
                        column: x => x.UomId,
                        principalTable: "UOM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityToProduce = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ProducedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SourceWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WipWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PlannedEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TransferStockEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrder", x => x.Id);
                    table.CheckConstraint("CK_WorkOrder_Quantities", "[QuantityToProduce] > 0.0000 AND [ProducedQuantity] >= 0.0000");
                    table.ForeignKey(
                        name: "FK_WorkOrder_BOM",
                        column: x => x.BomId,
                        principalTable: "BillOfMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrder_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrder_Item",
                        column: x => x.ProductionItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrder_SourceWarehouse",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrder_TargetWarehouse",
                        column: x => x.TargetWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrder_WipWarehouse",
                        column: x => x.WipWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BomOperation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    BomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkstationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DurationMinutes = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomOperation", x => x.Id);
                    table.CheckConstraint("CK_BomOperation_Duration", "[DurationMinutes] > 0.0000");
                    table.ForeignKey(
                        name: "FK_BomOperation_BillOfMaterials_BomId",
                        column: x => x.BomId,
                        principalTable: "BillOfMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BomOperation_Workstation",
                        column: x => x.WorkstationId,
                        principalTable: "Workstation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterials_ItemId",
                table: "BillOfMaterials",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterials_UomId",
                table: "BillOfMaterials",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_BOM_Tenant_Item",
                table: "BillOfMaterials",
                columns: new[] { "TenantId", "CompanyId", "ItemId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BomItem_BomId",
                table: "BomItem",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_BomItem_ItemId",
                table: "BomItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BomItem_UomId",
                table: "BomItem",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_BomOperation_BomId",
                table: "BomOperation",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_BomOperation_WorkstationId",
                table: "BomOperation",
                column: "WorkstationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_BomId",
                table: "WorkOrder",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_CompanyId",
                table: "WorkOrder",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_ProductionItemId",
                table: "WorkOrder",
                column: "ProductionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_SourceWarehouseId",
                table: "WorkOrder",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_TargetWarehouseId",
                table: "WorkOrder",
                column: "TargetWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_Tenant_Company_OrderNumber",
                table: "WorkOrder",
                columns: new[] { "TenantId", "CompanyId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_Tenant_Status",
                table: "WorkOrder",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrder_WipWarehouseId",
                table: "WorkOrder",
                column: "WipWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Workstation_CompanyId",
                table: "Workstation",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Workstation_Tenant_Company_Name",
                table: "Workstation",
                columns: new[] { "TenantId", "CompanyId", "WorkstationName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BomItem");

            migrationBuilder.DropTable(
                name: "BomOperation");

            migrationBuilder.DropTable(
                name: "WorkOrder");

            migrationBuilder.DropTable(
                name: "Workstation");

            migrationBuilder.DropTable(
                name: "BillOfMaterials")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BillOfMaterialsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
