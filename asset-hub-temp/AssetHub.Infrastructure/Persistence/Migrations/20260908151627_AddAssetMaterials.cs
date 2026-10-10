using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetMaterials",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetMaterials_Assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenant",
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetMaterials_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalSchema: "tenant",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaterials_AssetId",
                schema: "tenant",
                table: "AssetMaterials",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaterials_CatalogItemId",
                schema: "tenant",
                table: "AssetMaterials",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaterials_TenantId_AssetId",
                schema: "tenant",
                table: "AssetMaterials",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetMaterials_TenantId_AssetId_CatalogItemId",
                schema: "tenant",
                table: "AssetMaterials",
                columns: new[] { "TenantId", "AssetId", "CatalogItemId" },
                unique: true,
                filter: "\"IsDeleted\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetMaterials",
                schema: "tenant");
        }
    }
}
