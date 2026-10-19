using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmParityOpportunityItemsQuotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OpportunityId",
                table: "Quotations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseOpportunityAmount",
                table: "Opportunity",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Competitors",
                table: "Opportunity",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Opportunity",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<Guid>(
                name: "IndustryId",
                table: "Opportunity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LossReasonId",
                table: "Opportunity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketSegment",
                table: "Opportunity",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerritoryId",
                table: "Opportunity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Industry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Industry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpportunityItem_Item",
                        column: x => x.ItemId,
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpportunityItem_Opportunity",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityLossReason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityLossReason", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Territory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Territory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_OpportunityId",
                table: "Quotations",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunity_IndustryId",
                table: "Opportunity",
                column: "IndustryId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunity_LossReasonId",
                table: "Opportunity",
                column: "LossReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunity_TerritoryId",
                table: "Opportunity",
                column: "TerritoryId");

            migrationBuilder.CreateIndex(
                name: "UQ_Industry_Tenant_Code",
                table: "Industry",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityItem_ItemId",
                table: "OpportunityItem",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityItem_OpportunityId",
                table: "OpportunityItem",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityItem_Tenant_Opportunity",
                table: "OpportunityItem",
                columns: new[] { "TenantId", "OpportunityId" });

            migrationBuilder.CreateIndex(
                name: "UQ_OpportunityLossReason_Tenant_Code",
                table: "OpportunityLossReason",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Territory_Tenant_Code",
                table: "Territory",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_Industry",
                table: "Opportunity",
                column: "IndustryId",
                principalTable: "Industry",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_LossReason",
                table: "Opportunity",
                column: "LossReasonId",
                principalTable: "OpportunityLossReason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunity_Territory",
                table: "Opportunity",
                column: "TerritoryId",
                principalTable: "Territory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotation_Opportunity",
                table: "Quotations",
                column: "OpportunityId",
                principalTable: "Opportunity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_Industry",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_LossReason",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunity_Territory",
                table: "Opportunity");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotation_Opportunity",
                table: "Quotations");

            migrationBuilder.DropTable(
                name: "Industry");

            migrationBuilder.DropTable(
                name: "OpportunityItem");

            migrationBuilder.DropTable(
                name: "OpportunityLossReason");

            migrationBuilder.DropTable(
                name: "Territory");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_OpportunityId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Opportunity_IndustryId",
                table: "Opportunity");

            migrationBuilder.DropIndex(
                name: "IX_Opportunity_LossReasonId",
                table: "Opportunity");

            migrationBuilder.DropIndex(
                name: "IX_Opportunity_TerritoryId",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "OpportunityId",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "BaseOpportunityAmount",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "Competitors",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "IndustryId",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "LossReasonId",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "MarketSegment",
                table: "Opportunity");

            migrationBuilder.DropColumn(
                name: "TerritoryId",
                table: "Opportunity");
        }
    }
}
