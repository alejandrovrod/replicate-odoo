using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddDesignJsonToTemplateTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayoutType",
                schema: "tenant",
                table: "CommunicationTemplateTranslations");

            migrationBuilder.RenameColumn(
                name: "RawContent",
                schema: "tenant",
                table: "CommunicationTemplateTranslations",
                newName: "DesignJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DesignJson",
                schema: "tenant",
                table: "CommunicationTemplateTranslations",
                newName: "RawContent");

            migrationBuilder.AddColumn<string>(
                name: "LayoutType",
                schema: "tenant",
                table: "CommunicationTemplateTranslations",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
