using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddAssetHealthPredictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetHealthPredictions",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskProbability = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PredictedFailureDays = table.Column<int>(type: "int", nullable: true),
                    TopFeatureContributionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetHealthPredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetHealthPredictions_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetHealthPredictions_AssetId",
                schema: "tenant",
                table: "AssetHealthPredictions",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetHealthPredictions_CreatedAt",
                schema: "tenant",
                table: "AssetHealthPredictions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AssetHealthPredictions_TenantId_AssetId",
                schema: "tenant",
                table: "AssetHealthPredictions",
                columns: new[] { "TenantId", "AssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetHealthPredictions",
                schema: "tenant");
        }
    }
}
