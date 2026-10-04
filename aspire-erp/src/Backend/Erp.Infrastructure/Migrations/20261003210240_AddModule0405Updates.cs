using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModule0405Updates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Item_ExpenseAccount",
                table: "Item");

            migrationBuilder.DropForeignKey(
                name: "FK_Item_IncomeAccount",
                table: "Item");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoice_PurchaseReceipt",
                table: "PurchaseInvoice");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouse_StockAccount",
                table: "Warehouse");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLine");

            migrationBuilder.DropIndex(
                name: "IX_UOM_Tenant_Code",
                table: "UOM");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UOM_ToBaseFactor",
                table: "UOM");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceiptLine_Qty_Positive",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceiptLine_Rate_Positive",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_Tenant_Company_Voucher",
                table: "PurchaseOrder");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoice_Tenant_Receipt_Unique",
                table: "PurchaseInvoice");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoice_TaxAmount_NonNegative",
                table: "PurchaseInvoice");

            migrationBuilder.DropIndex(
                name: "IX_Item_ExpenseAccountId",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "UOM");

            migrationBuilder.DropColumn(
                name: "ToBaseFactor",
                table: "UOM");

            migrationBuilder.DropColumn(
                name: "VoucherNo",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "Item");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Warehouse",
                newName: "WarehouseName");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Warehouse",
                newName: "WarehouseCode");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "UOM",
                newName: "UomName");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseReceipt_Tenant_Company_Voucher",
                table: "PurchaseReceipt",
                newName: "IX_PurchaseReceipt_Tenant_Company_VoucherNo");

            migrationBuilder.RenameColumn(
                name: "PostingDate",
                table: "PurchaseOrder",
                newName: "TransactionDate");

            migrationBuilder.RenameColumn(
                name: "PurchaseReceiptId",
                table: "PurchaseInvoice",
                newName: "SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseInvoice_PurchaseReceiptId",
                table: "PurchaseInvoice",
                newName: "IX_PurchaseInvoice_SupplierId");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Item",
                newName: "ItemName");

            migrationBuilder.RenameColumn(
                name: "IncomeAccountId",
                table: "Item",
                newName: "DefaultWarehouseId");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Item",
                newName: "ItemCode");

            migrationBuilder.RenameColumn(
                name: "BaseUOMId",
                table: "Item",
                newName: "StockUomId");

            migrationBuilder.RenameIndex(
                name: "IX_Item_IncomeAccountId",
                table: "Item",
                newName: "IX_Item_DefaultWarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_Item_BaseUOMId",
                table: "Item",
                newName: "IX_Item_StockUomId");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "Warehouse",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Warehouse",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "UOM",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "UOM",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustBeWholeNumber",
                table: "UOM",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "UOM",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BillingCurrency",
                table: "Supplier",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPayableAccountId",
                table: "Supplier",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OutstandingAmount",
                table: "Supplier",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermsDays",
                table: "Supplier",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "Supplier",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "StockLedgerEntry",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "StockEntry",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "PurchaseReceiptLine",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "PurchaseReceiptLine",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "VoucherNo",
                table: "PurchaseReceipt",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseReceipt",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PurchaseReceipt",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "PurchaseReceipt",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "PurchaseReceipt",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "PurchaseOrder",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<decimal>(
                name: "BilledPercentage",
                table: "PurchaseOrder",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrandTotal",
                table: "PurchaseOrder",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetTotal",
                table: "PurchaseOrder",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "PurchaseOrder",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedPercentage",
                table: "PurchaseOrder",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduleDate",
                table: "PurchaseOrder",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "TaxTotal",
                table: "PurchaseOrder",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "PurchaseInvoiceLine",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "PurchaseInvoiceLine",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BillNumber",
                table: "PurchaseInvoice",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "PurchaseInvoice",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "GrandTotal",
                table: "PurchaseInvoice",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetTotal",
                table: "PurchaseInvoice",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<decimal>(
                name: "OutstandingAmount",
                table: "PurchaseInvoice",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseInvoice",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PurchaseInvoice",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxTotal",
                table: "PurchaseInvoice",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxTotal",
                table: "PurchaseInvoice",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0.0000m);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Item",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Item",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStockItem",
                table: "Item",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SafetyStock",
                table: "Item",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardSellingRate",
                table: "Item",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CogsAccountCode",
                table: "Company",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultIncomeAccountCode",
                table: "Company",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultReceivableAccountCode",
                table: "Company",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DefaultReceivableAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    BypassCreditLimitCheck = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    BillingCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => x.Id);
                    table.CheckConstraint("CK_Customer_CreditLimit", "[CreditLimit] >= 0.0000");
                    table.ForeignKey(
                        name: "FK_Customer_Account",
                        column: x => x.DefaultReceivableAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Customer_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "POSProfile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardClearingAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncomeAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WriteOffAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POSProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POSProfile_CardAccount",
                        column: x => x.CardClearingAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POSProfile_CashAccount",
                        column: x => x.CashAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POSProfile_IncomeAccount",
                        column: x => x.IncomeAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POSProfile_Warehouse",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POSProfile_WriteOffAccount",
                        column: x => x.WriteOffAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0.0000m),
                    BilledQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0.0000m),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItem", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrderItem_Amount", "[Amount] >= 0.0000");
                    table.CheckConstraint("CK_PurchaseOrderItem_Quantity", "[Quantity] > 0.0000");
                    table.CheckConstraint("CK_PurchaseOrderItem_Rate", "[Rate] >= 0.0000");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItem_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItem_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsPOS = table.Column<bool>(type: "bit", nullable: false),
                    UpdateStock = table.Column<bool>(type: "bit", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NetTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoice_Customer",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoice_SourceWarehouse",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    DeliveredPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    BilledPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrder", x => x.Id);
                    table.CheckConstraint("CK_SalesOrder_Totals", "[NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000");
                    table.ForeignKey(
                        name: "FK_SalesOrder_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrder_Customer",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoiceItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    SalesInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoiceItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItem_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceItem_SalesInvoice_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNote_Company",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNote_SalesOrder",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNote_Warehouse",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DeliveredQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    BilledQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderItem", x => x.Id);
                    table.CheckConstraint("CK_SalesOrderItem_Amount", "[Amount] >= 0.0000");
                    table.CheckConstraint("CK_SalesOrderItem_Quantity", "[Quantity] > 0.0000");
                    table.CheckConstraint("CK_SalesOrderItem_Rate", "[Rate] >= 0.0000");
                    table.ForeignKey(
                        name: "FK_SalesOrderItem_Header",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesOrderItem_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNoteLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNoteLine", x => x.Id);
                    table.CheckConstraint("CK_DeliveryNoteLine_Qty", "[Qty] > 0.0000");
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLine_Header",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLine_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLine_OrderLine",
                        column: x => x.SalesOrderItemId,
                        principalTable: "SalesOrderItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Supplier_DefaultPayableAccountId",
                table: "Supplier",
                column: "DefaultPayableAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceiptLine_Amount",
                table: "PurchaseReceiptLine",
                sql: "[Amount] >= 0.0000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceiptLine_Quantity",
                table: "PurchaseReceiptLine",
                sql: "[Qty] > 0.0000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceiptLine_Rate",
                table: "PurchaseReceiptLine",
                sql: "[Rate] >= 0.0000");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipt_SupplierId",
                table: "PurchaseReceipt",
                column: "SupplierId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceipt_TotalAmount",
                table: "PurchaseReceipt",
                sql: "[TotalAmount] >= 0.0000");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_Tenant_Company_OrderNumber",
                table: "PurchaseOrder",
                columns: new[] { "TenantId", "CompanyId", "OrderNumber" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrder_Totals",
                table: "PurchaseOrder",
                sql: "[NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Amount_NonNegative",
                table: "PurchaseInvoiceLine",
                sql: "[Amount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice",
                sql: "[NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_CompanyId",
                table: "Customer",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_DefaultReceivableAccountId",
                table: "Customer",
                column: "DefaultReceivableAccountId");

            migrationBuilder.CreateIndex(
                name: "UQ_Customer_Tenant_Company_Code",
                table: "Customer",
                columns: new[] { "TenantId", "CompanyId", "CustomerCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNote_CompanyId",
                table: "DeliveryNote",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNote_SalesOrderId",
                table: "DeliveryNote",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNote_WarehouseId",
                table: "DeliveryNote",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UQ_DeliveryNote_Tenant_Company_VoucherNo",
                table: "DeliveryNote",
                columns: new[] { "TenantId", "CompanyId", "VoucherNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLine_DeliveryNoteId",
                table: "DeliveryNoteLine",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLine_ItemId",
                table: "DeliveryNoteLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLine_SalesOrderItemId",
                table: "DeliveryNoteLine",
                column: "SalesOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_POSProfile_CardClearingAccountId",
                table: "POSProfile",
                column: "CardClearingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_POSProfile_CashAccountId",
                table: "POSProfile",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_POSProfile_IncomeAccountId",
                table: "POSProfile",
                column: "IncomeAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_POSProfile_WarehouseId",
                table: "POSProfile",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_POSProfile_WriteOffAccountId",
                table: "POSProfile",
                column: "WriteOffAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItem_ItemId",
                table: "PurchaseOrderItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItem_PurchaseOrderId",
                table: "PurchaseOrderItem",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoice_CustomerId",
                table: "SalesInvoice",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoice_SourceWarehouseId",
                table: "SalesInvoice",
                column: "SourceWarehouseId");

            migrationBuilder.CreateIndex(
                name: "UQ_SalesInvoice_Tenant_Company_InvoiceNo",
                table: "SalesInvoice",
                columns: new[] { "TenantId", "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItem_ItemId",
                table: "SalesInvoiceItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceItem_SalesInvoiceId",
                table: "SalesInvoiceItem",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrder_CompanyId",
                table: "SalesOrder",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrder_CustomerId",
                table: "SalesOrder",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrder_Tenant_Company_Order",
                table: "SalesOrder",
                columns: new[] { "TenantId", "CompanyId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItem_ItemId",
                table: "SalesOrderItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItem_SalesOrderId",
                table: "SalesOrderItem",
                column: "SalesOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Item_Warehouse_DefaultWarehouseId",
                table: "Item",
                column: "DefaultWarehouseId",
                principalTable: "Warehouse",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoice_Supplier",
                table: "PurchaseInvoice",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReceipt_Supplier",
                table: "PurchaseReceipt",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Supplier_Account",
                table: "Supplier",
                column: "DefaultPayableAccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouse_Account_AccountId",
                table: "Warehouse",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Item_Warehouse_DefaultWarehouseId",
                table: "Item");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoice_Supplier",
                table: "PurchaseInvoice");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReceipt_Supplier",
                table: "PurchaseReceipt");

            migrationBuilder.DropForeignKey(
                name: "FK_Supplier_Account",
                table: "Supplier");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouse_Account_AccountId",
                table: "Warehouse");

            migrationBuilder.DropTable(
                name: "DeliveryNoteLine");

            migrationBuilder.DropTable(
                name: "POSProfile");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItem");

            migrationBuilder.DropTable(
                name: "SalesInvoiceItem");

            migrationBuilder.DropTable(
                name: "DeliveryNote");

            migrationBuilder.DropTable(
                name: "SalesOrderItem");

            migrationBuilder.DropTable(
                name: "SalesInvoice");

            migrationBuilder.DropTable(
                name: "SalesOrder");

            migrationBuilder.DropTable(
                name: "Customer")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "CustomerHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropIndex(
                name: "IX_Supplier_DefaultPayableAccountId",
                table: "Supplier");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceiptLine_Amount",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceiptLine_Quantity",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceiptLine_Rate",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReceipt_SupplierId",
                table: "PurchaseReceipt");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceipt_TotalAmount",
                table: "PurchaseReceipt");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_Tenant_Company_OrderNumber",
                table: "PurchaseOrder");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrder_Totals",
                table: "PurchaseOrder");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoiceLine_Amount_NonNegative",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoice_Totals",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Warehouse");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UOM");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "UOM");

            migrationBuilder.DropColumn(
                name: "MustBeWholeNumber",
                table: "UOM");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "UOM");

            migrationBuilder.DropColumn(
                name: "BillingCurrency",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "DefaultPayableAccountId",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "OutstandingAmount",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "PaymentTermsDays",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "Supplier");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "StockLedgerEntry");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "StockEntry");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "PurchaseReceiptLine");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "PurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "PurchaseReceipt");

            migrationBuilder.DropColumn(
                name: "BilledPercentage",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "GrandTotal",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "NetTotal",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ReceivedPercentage",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ScheduleDate",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "TaxTotal",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "BillNumber",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "GrandTotal",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "NetTotal",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "OutstandingAmount",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "TaxTotal",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxTotal",
                table: "PurchaseInvoice");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "IsStockItem",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "SafetyStock",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "StandardSellingRate",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "CogsAccountCode",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "DefaultIncomeAccountCode",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "DefaultReceivableAccountCode",
                table: "Company");

            migrationBuilder.RenameColumn(
                name: "WarehouseName",
                table: "Warehouse",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "WarehouseCode",
                table: "Warehouse",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "UomName",
                table: "UOM",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseReceipt_Tenant_Company_VoucherNo",
                table: "PurchaseReceipt",
                newName: "IX_PurchaseReceipt_Tenant_Company_Voucher");

            migrationBuilder.RenameColumn(
                name: "TransactionDate",
                table: "PurchaseOrder",
                newName: "PostingDate");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "PurchaseInvoice",
                newName: "PurchaseReceiptId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseInvoice_SupplierId",
                table: "PurchaseInvoice",
                newName: "IX_PurchaseInvoice_PurchaseReceiptId");

            migrationBuilder.RenameColumn(
                name: "StockUomId",
                table: "Item",
                newName: "BaseUOMId");

            migrationBuilder.RenameColumn(
                name: "ItemName",
                table: "Item",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "ItemCode",
                table: "Item",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "DefaultWarehouseId",
                table: "Item",
                newName: "IncomeAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Item_StockUomId",
                table: "Item",
                newName: "IX_Item_BaseUOMId");

            migrationBuilder.RenameIndex(
                name: "IX_Item_DefaultWarehouseId",
                table: "Item",
                newName: "IX_Item_IncomeAccountId");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "Warehouse",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "UOM",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ToBaseFactor",
                table: "UOM",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "PurchaseReceiptLine",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AlterColumn<string>(
                name: "VoucherNo",
                table: "PurchaseReceipt",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PurchaseOrder",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VoucherNo",
                table: "PurchaseOrder",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "PurchaseInvoiceLine",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "PurchaseInvoice",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "Item",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLine", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrderLine_Qty_Positive", "[Qty] > 0");
                    table.CheckConstraint("CK_PurchaseOrderLine_Rate_Positive", "[Rate] > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLine_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLine_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UOM_Tenant_Code",
                table: "UOM",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UOM_ToBaseFactor",
                table: "UOM",
                sql: "[ToBaseFactor] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceiptLine_Qty_Positive",
                table: "PurchaseReceiptLine",
                sql: "[Qty] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceiptLine_Rate_Positive",
                table: "PurchaseReceiptLine",
                sql: "[Rate] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_Tenant_Company_Voucher",
                table: "PurchaseOrder",
                columns: new[] { "TenantId", "CompanyId", "VoucherNo" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_Tenant_Receipt_Unique",
                table: "PurchaseInvoice",
                columns: new[] { "TenantId", "PurchaseReceiptId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoice_TaxAmount_NonNegative",
                table: "PurchaseInvoice",
                sql: "[TaxAmount] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Item_ExpenseAccountId",
                table: "Item",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_ItemId",
                table: "PurchaseOrderLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLine_PurchaseOrderId",
                table: "PurchaseOrderLine",
                column: "PurchaseOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Item_ExpenseAccount",
                table: "Item",
                column: "ExpenseAccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Item_IncomeAccount",
                table: "Item",
                column: "IncomeAccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoice_PurchaseReceipt",
                table: "PurchaseInvoice",
                column: "PurchaseReceiptId",
                principalTable: "PurchaseReceipt",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouse_StockAccount",
                table: "Warehouse",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
