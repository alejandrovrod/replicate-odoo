using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAndGeneralLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeStock",
                table: "Company",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodLockDate",
                table: "Company",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockReceivedAccountCode",
                table: "Company",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GLEntry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Credit = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    VoucherType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLEntry", x => x.Id);
                    table.CheckConstraint("CK_Credit_Positive", "[Credit] >= 0");
                    table.CheckConstraint("CK_Debit_Positive", "[Debit] >= 0");
                    table.ForeignKey(
                        name: "FK_GLEntry_Account",
                        column: x => x.AccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResponseStatus = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UOM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToBaseFactor = table.Column<decimal>(type: "decimal(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UOM", x => x.Id);
                    table.CheckConstraint("CK_UOM_ToBaseFactor", "[ToBaseFactor] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Warehouse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ParentWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsGroup = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warehouse_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Warehouse_Parent",
                        column: x => x.ParentWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Warehouse_StockAccount",
                        column: x => x.AccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Item",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ValuationMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BaseUOMId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncomeAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Item_ExpenseAccount",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Item_IncomeAccount",
                        column: x => x.IncomeAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Item_UOM",
                        column: x => x.BaseUOMId,
                        principalTable: "UOM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockEntry_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockEntry_TargetWarehouse",
                        column: x => x.TargetWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockEntry_Warehouse",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockEntryItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    StockEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockEntryItem", x => x.Id);
                    table.CheckConstraint("CK_StockEntryItem_Qty_Positive", "[Qty] > 0");
                    table.ForeignKey(
                        name: "FK_StockEntryItem_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockEntryItem_StockEntry_StockEntryId",
                        column: x => x.StockEntryId,
                        principalTable: "StockEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockLedgerEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    QtyChange = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ValuationRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLedgerEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockLedgerEntry_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockLedgerEntry_StockEntry",
                        column: x => x.StockEntryId,
                        principalTable: "StockEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockLedgerEntry_Warehouse",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GLEntry_AccountId",
                table: "GLEntry",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GLEntry_Tenant_Account_Date",
                table: "GLEntry",
                columns: new[] { "TenantId", "AccountId", "PostingDate" })
                .Annotation("SqlServer:Include", new[] { "Debit", "Credit", "VoucherType", "VoucherNo" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecord_Tenant_Key",
                table: "IdempotencyRecord",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Item_BaseUOMId",
                table: "Item",
                column: "BaseUOMId");

            migrationBuilder.CreateIndex(
                name: "IX_Item_ExpenseAccountId",
                table: "Item",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Item_IncomeAccountId",
                table: "Item",
                column: "IncomeAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Item_Tenant_Code",
                table: "Item",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockEntry_CompanyId",
                table: "StockEntry",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_StockEntry_TargetWarehouseId",
                table: "StockEntry",
                column: "TargetWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockEntry_Tenant_Company_Voucher",
                table: "StockEntry",
                columns: new[] { "TenantId", "CompanyId", "VoucherNo" });

            migrationBuilder.CreateIndex(
                name: "IX_StockEntry_WarehouseId",
                table: "StockEntry",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockEntryItem_ItemId",
                table: "StockEntryItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockEntryItem_StockEntryId",
                table: "StockEntryItem",
                column: "StockEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedger_Tenant_Item_Warehouse_Date",
                table: "StockLedgerEntry",
                columns: new[] { "TenantId", "ItemId", "WarehouseId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgerEntry_ItemId",
                table: "StockLedgerEntry",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgerEntry_StockEntryId",
                table: "StockLedgerEntry",
                column: "StockEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgerEntry_WarehouseId",
                table: "StockLedgerEntry",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_UOM_Tenant_Code",
                table: "UOM",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouse_CompanyId",
                table: "Warehouse",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouse_ParentWarehouseId",
                table: "Warehouse",
                column: "ParentWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouse_StockAccountId",
                table: "Warehouse",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouse_Tenant_Company_Code",
                table: "Warehouse",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true);

            // Constitution Article III.2 (append-only General Ledger), SECOND line of defence:
            // AppDbContext already throws GLEntryAppendOnlyViolationException on any Modified/
            // Deleted GLEntry, but that guard does not cover raw SQL (sqlcmd, SSMS, ETL jobs).
            // An INSTEAD OF UPDATE/DELETE trigger rejects those too. INSERT is untouched: the
            // ledger grows, it never mutates.
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER dbo.trg_GLEntry_AppendOnly
                ON dbo.GLEntry
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'GLEntry is append-only', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_GLEntry_AppendOnly;");

            migrationBuilder.DropTable(
                name: "GLEntry");

            migrationBuilder.DropTable(
                name: "IdempotencyRecord");

            migrationBuilder.DropTable(
                name: "StockEntryItem");

            migrationBuilder.DropTable(
                name: "StockLedgerEntry");

            migrationBuilder.DropTable(
                name: "Item");

            migrationBuilder.DropTable(
                name: "StockEntry");

            migrationBuilder.DropTable(
                name: "UOM");

            migrationBuilder.DropTable(
                name: "Warehouse");

            migrationBuilder.DropColumn(
                name: "AllowNegativeStock",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "PeriodLockDate",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "StockReceivedAccountCode",
                table: "Company");
        }
    }
}

