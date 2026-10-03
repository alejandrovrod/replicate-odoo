using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <summary>
    /// tasks.md 2.2 (plan.md §2 "FrozenAccountsDate DATE NULL"): renames Company.PeriodLockDate
    /// to the plan's literal FrozenAccountsDate. Company is a SYSTEM_VERSIONED temporal table
    /// (Constitution IV.2), so if SQL Server rejects the rename while versioning is ON, this
    /// operation is replaced in place by the SYSTEM_VERSIONING OFF / sp_rename / ON dance used
    /// by AddOptimisticConcurrencyRowVersion (keep the dance hand-written - regeneration loses it).
    /// </summary>
    /// <inheritdoc />
    public partial class RenamePeriodLockDateToFrozenAccountsDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PeriodLockDate",
                table: "Company",
                newName: "FrozenAccountsDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FrozenAccountsDate",
                table: "Company",
                newName: "PeriodLockDate");
        }
    }
}
