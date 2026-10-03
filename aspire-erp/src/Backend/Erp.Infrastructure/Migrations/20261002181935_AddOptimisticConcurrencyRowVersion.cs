using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimisticConcurrencyRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockEntry",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseOrder",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Item",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // Account is a SYSTEM_VERSIONED temporal table (Constitution IV.2), and SQL Server
            // rejects `ALTER TABLE ... ADD rowversion` on it: the ALTER is mirrored to
            // AccountHistory via an internal UPDATE that cannot assign a rowversion value
            // (error 515: "Cannot insert the value NULL into column 'RowVersion'"). Workaround
            // (verified manually on SQL Server 2025): add the column to both tables while
            // system-versioning is OFF, then re-enable it. Keep this hand-written - regenerating
            // the migration would lose the dance.
            migrationBuilder.Sql("ALTER TABLE [Account] SET (SYSTEM_VERSIONING = OFF);");
            migrationBuilder.Sql("ALTER TABLE [Account] ADD [RowVersion] rowversion;");
            migrationBuilder.Sql("ALTER TABLE [AccountHistory] ADD [RowVersion] rowversion;");
            migrationBuilder.Sql(
                "ALTER TABLE [Account] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.[AccountHistory]));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockEntry");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Item");

            // Reverse of the temporal dance used in Up() - same SQL Server limitation applies
            // to DROP on a SYSTEM_VERSIONED table.
            migrationBuilder.Sql("ALTER TABLE [Account] SET (SYSTEM_VERSIONING = OFF);");
            migrationBuilder.Sql("ALTER TABLE [Account] DROP COLUMN [RowVersion];");
            migrationBuilder.Sql("ALTER TABLE [AccountHistory] DROP COLUMN [RowVersion];");
            migrationBuilder.Sql(
                "ALTER TABLE [Account] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.[AccountHistory]));");
        }
    }
}
