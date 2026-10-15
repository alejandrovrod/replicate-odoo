using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Company",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                table: "BankAccount",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SwiftBic = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BanksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccount_BankId",
                table: "BankAccount",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccount_Tenant_Bank",
                table: "BankAccount",
                columns: new[] { "TenantId", "BankId" });

            migrationBuilder.CreateIndex(
                name: "IX_Banks_Tenant_Country_Active",
                table: "Banks",
                columns: new[] { "TenantId", "CountryCode", "IsActive" })
                .Annotation("SqlServer:Include", new[] { "Code", "Name" });

            migrationBuilder.CreateIndex(
                name: "UQ_Banks_Tenant_Country_Code",
                table: "Banks",
                columns: new[] { "TenantId", "CountryCode", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccount_Bank",
                table: "BankAccount",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccount_Bank",
                table: "BankAccount");

            migrationBuilder.DropTable(
                name: "Banks")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "BanksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropIndex(
                name: "IX_BankAccount_BankId",
                table: "BankAccount");

            migrationBuilder.DropIndex(
                name: "IX_BankAccount_Tenant_Bank",
                table: "BankAccount");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Company");

            migrationBuilder.DropColumn(
                name: "BankId",
                table: "BankAccount");
        }
    }
}
