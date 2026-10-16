using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLandedCostVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LandedCostVoucher",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    DistributeBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Amount"),
                    TotalTaxesAndCharges = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCostVoucher", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LandedCostApportionmentItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    LandedCostVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OriginalRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    ApplicableCharges = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    NewRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCostApportionmentItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostItem_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandedCostItem_Receipt",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "PurchaseReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandedCostItem_Voucher",
                        column: x => x.LandedCostVoucherId,
                        principalTable: "LandedCostVoucher",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandedCostChargeItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    LandedCostVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCostChargeItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostCharge_Account",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandedCostCharge_Voucher",
                        column: x => x.LandedCostVoucherId,
                        principalTable: "LandedCostVoucher",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandedCostReceiptReference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    LandedCostVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCostReceiptReference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostReceipt_Receipt",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "PurchaseReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandedCostReceipt_Voucher",
                        column: x => x.LandedCostVoucherId,
                        principalTable: "LandedCostVoucher",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostApportionmentItem_ItemId",
                table: "LandedCostApportionmentItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostApportionmentItem_PurchaseReceiptId",
                table: "LandedCostApportionmentItem",
                column: "PurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostItem_VoucherId",
                table: "LandedCostApportionmentItem",
                column: "LandedCostVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostChargeItem_ExpenseAccountId",
                table: "LandedCostChargeItem",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostChargeItem_LandedCostVoucherId",
                table: "LandedCostChargeItem",
                column: "LandedCostVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostReceiptReference_LandedCostVoucherId",
                table: "LandedCostReceiptReference",
                column: "LandedCostVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostReceiptReference_PurchaseReceiptId",
                table: "LandedCostReceiptReference",
                column: "PurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostVoucher_Tenant_Company_Number",
                table: "LandedCostVoucher",
                columns: new[] { "TenantId", "CompanyId", "VoucherNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandedCostApportionmentItem");

            migrationBuilder.DropTable(
                name: "LandedCostChargeItem");

            migrationBuilder.DropTable(
                name: "LandedCostReceiptReference");

            migrationBuilder.DropTable(
                name: "LandedCostVoucher");
        }
    }
}
