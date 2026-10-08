using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankAccountRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [BankAccount] SET (SYSTEM_VERSIONING = OFF);");
            migrationBuilder.Sql("DROP TABLE [BankAccountHistory];");
            migrationBuilder.Sql("ALTER TABLE [BankAccount] ADD [RowVersion] rowversion NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE [BankAccount] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[BankAccountHistory]));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BankAccount");
        }
    }
}
