using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCatalogTargetModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetModulesJson",
                schema: "tenant",
                table: "Catalogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetModulesJson",
                schema: "tenant",
                table: "Catalogs");
        }
    }
}
