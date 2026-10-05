using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadDedupUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lead_CompanyId",
                table: "Lead");

            migrationBuilder.CreateIndex(
                name: "UQ_Lead_Company_Source_ExternalRef",
                table: "Lead",
                columns: new[] { "CompanyId", "Source", "ExternalReference" },
                unique: true,
                filter: "[ExternalReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Lead_Company_Source_ExternalRef",
                table: "Lead");

            migrationBuilder.CreateIndex(
                name: "IX_Lead_CompanyId",
                table: "Lead",
                column: "CompanyId");
        }
    }
}
