using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSubdomainOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing tenants predate subdomain onboarding. Add the column nullable, backfill a
            // deterministic DNS label, then tighten it to NOT NULL before creating the unique
            // index (a NOT NULL + default "" column would leave every legacy row colliding).
            migrationBuilder.AddColumn<string>(
                name: "Subdomain",
                table: "Tenant",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE dbo.Tenant
                SET Subdomain = LOWER(LTRIM(RTRIM(Code)))
                WHERE Subdomain IS NULL
                  AND LEN(LTRIM(RTRIM(Code))) BETWEEN 3 AND 32
                  AND LTRIM(RTRIM(Code)) NOT LIKE '%[^a-zA-Z0-9-]%'
                  AND LTRIM(RTRIM(Code)) NOT LIKE '-%'
                  AND LTRIM(RTRIM(Code)) NOT LIKE '%-';
                """);

            // Any legacy code that is not already a legal label gets a stable, unique fallback
            // derived from the tenant id.
            migrationBuilder.Sql(
                """
                UPDATE dbo.Tenant
                SET Subdomain = CONCAT(
                    'tenant-',
                    LOWER(LEFT(REPLACE(CONVERT(nvarchar(36), Id), '-', ''), 8)))
                WHERE Subdomain IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Subdomain",
                table: "Tenant",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Tenants_Subdomain",
                table: "Tenant",
                column: "Subdomain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Tenants_Subdomain",
                table: "Tenant");

            migrationBuilder.DropColumn(
                name: "Subdomain",
                table: "Tenant");
        }
    }
}
