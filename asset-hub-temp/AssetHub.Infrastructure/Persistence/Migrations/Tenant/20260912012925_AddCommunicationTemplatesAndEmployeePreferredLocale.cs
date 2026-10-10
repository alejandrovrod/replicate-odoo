using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCommunicationTemplatesAndEmployeePreferredLocale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredLocale",
                schema: "tenant",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "es");

            migrationBuilder.CreateTable(
                name: "CommunicationTemplates",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityScope = table.Column<int>(type: "int", nullable: false),
                    TemplateType = table.Column<int>(type: "int", nullable: false),
                    ActiveVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationTemplateVersions",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationTemplateVersions_CommunicationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "tenant",
                        principalTable: "CommunicationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationTemplateTranslations",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationTemplateTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationTemplateTranslations_CommunicationTemplateVersions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "tenant",
                        principalTable: "CommunicationTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplates_ActiveVersionId",
                schema: "tenant",
                table: "CommunicationTemplates",
                column: "ActiveVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplates_TenantId_Code",
                schema: "tenant",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplates_TenantId_EntityScope_TemplateType",
                schema: "tenant",
                table: "CommunicationTemplates",
                columns: new[] { "TenantId", "EntityScope", "TemplateType" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplateTranslations_VersionId_Locale",
                schema: "tenant",
                table: "CommunicationTemplateTranslations",
                columns: new[] { "VersionId", "Locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplateVersions_TemplateId_VersionNumber",
                schema: "tenant",
                table: "CommunicationTemplateVersions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationTemplates_CommunicationTemplateVersions_ActiveVersionId",
                schema: "tenant",
                table: "CommunicationTemplates",
                column: "ActiveVersionId",
                principalSchema: "tenant",
                principalTable: "CommunicationTemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationTemplates_CommunicationTemplateVersions_ActiveVersionId",
                schema: "tenant",
                table: "CommunicationTemplates");

            migrationBuilder.DropTable(
                name: "CommunicationTemplateTranslations",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "CommunicationTemplateVersions",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "CommunicationTemplates",
                schema: "tenant");

            migrationBuilder.DropColumn(
                name: "PreferredLocale",
                schema: "tenant",
                table: "Employees");
        }
    }
}
