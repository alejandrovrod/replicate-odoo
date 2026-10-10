using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "tenant",
                table: "MaintenanceParts",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                schema: "tenant",
                table: "MaintenanceParts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSupplierName",
                schema: "tenant",
                table: "MaintenanceParts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryTransactionId",
                schema: "tenant",
                table: "MaintenanceParts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                schema: "tenant",
                table: "MaintenanceParts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "tenant",
                table: "MaintenanceParts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantInventorySettings",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    OperatingMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInventorySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryTransactions_ReversalOfId",
                        column: x => x.ReversalOfId,
                        principalSchema: "tenant",
                        principalTable: "InventoryTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_MaintenanceOrders_MaintenanceOrderId",
                        column: x => x.MaintenanceOrderId,
                        principalSchema: "tenant",
                        principalTable: "MaintenanceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "tenant",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageUnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockBalances_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "tenant",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceParts_InventoryTransactionId",
                schema: "tenant",
                table: "MaintenanceParts",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceParts_WarehouseId",
                schema: "tenant",
                table: "MaintenanceParts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CatalogItemId",
                schema: "tenant",
                table: "InventoryTransactions",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_MaintenanceOrderId",
                schema: "tenant",
                table: "InventoryTransactions",
                column: "MaintenanceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReversalOfId",
                schema: "tenant",
                table: "InventoryTransactions",
                column: "ReversalOfId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_IdempotencyKey",
                schema: "tenant",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_WarehouseId",
                schema: "tenant",
                table: "InventoryTransactions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_CatalogItemId",
                schema: "tenant",
                table: "StockBalances",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_TenantId_WarehouseId_CatalogItemId",
                schema: "tenant",
                table: "StockBalances",
                columns: new[] { "TenantId", "WarehouseId", "CatalogItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_WarehouseId",
                schema: "tenant",
                table: "StockBalances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInventorySettings_TenantId",
                schema: "tenant",
                table: "TenantInventorySettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code_TenantId",
                schema: "tenant",
                table: "Warehouses",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceParts_InventoryTransactions_InventoryTransactionId",
                schema: "tenant",
                table: "MaintenanceParts",
                column: "InventoryTransactionId",
                principalSchema: "tenant",
                principalTable: "InventoryTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceParts_Warehouses_WarehouseId",
                schema: "tenant",
                table: "MaintenanceParts",
                column: "WarehouseId",
                principalSchema: "tenant",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceParts_InventoryTransactions_InventoryTransactionId",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceParts_Warehouses_WarehouseId",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropTable(
                name: "InventoryTransactions",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "StockBalances",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "TenantInventorySettings",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "Warehouses",
                schema: "tenant");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceParts_InventoryTransactionId",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceParts_WarehouseId",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropColumn(
                name: "ExternalSupplierName",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropColumn(
                name: "InventoryTransactionId",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "tenant",
                table: "MaintenanceParts");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "tenant",
                table: "MaintenanceParts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}
