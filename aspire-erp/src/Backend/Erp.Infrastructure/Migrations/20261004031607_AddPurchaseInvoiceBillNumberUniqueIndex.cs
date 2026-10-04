using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseInvoiceBillNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoice_CompanyId",
                table: "PurchaseInvoice");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_Company_BillNumber",
                table: "PurchaseInvoice",
                columns: new[] { "CompanyId", "BillNumber" },
                unique: true,
                filter: "[BillNumber] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoice_Company_BillNumber",
                table: "PurchaseInvoice");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoice_CompanyId",
                table: "PurchaseInvoice",
                column: "CompanyId");
        }
    }
}
