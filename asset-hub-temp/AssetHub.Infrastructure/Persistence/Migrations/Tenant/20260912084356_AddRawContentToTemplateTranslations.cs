using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRawContentToTemplateTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LayoutType",
                schema: "tenant",
                table: "CommunicationTemplateTranslations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawContent",
                schema: "tenant",
                table: "CommunicationTemplateTranslations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayoutType",
                schema: "tenant",
                table: "CommunicationTemplateTranslations");

            migrationBuilder.DropColumn(
                name: "RawContent",
                schema: "tenant",
                table: "CommunicationTemplateTranslations");
        }
    }
}
