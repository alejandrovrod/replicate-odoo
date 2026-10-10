using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddIncidentTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertiesJson",
                schema: "tenant",
                table: "Incidents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "IncidentTemplates",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LifecycleStates = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                column: "IncidentTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTemplates_Code_Version_TenantId",
                schema: "tenant",
                table: "IncidentTemplates",
                columns: new[] { "Code", "Version", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents",
                column: "IncidentTemplateId",
                principalSchema: "tenant",
                principalTable: "IncidentTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_IncidentTemplates_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropTable(
                name: "IncidentTemplates",
                schema: "tenant");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_IncidentTemplateId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "IncidentTemplateId",
                schema: "tenant",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "PropertiesJson",
                schema: "tenant",
                table: "Incidents");
        }
    }
}
