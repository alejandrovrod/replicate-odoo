using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFxGainLossAccountSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO Account (Id, TenantId, CompanyId, AccountCode, AccountName, RootType, Type, IsGroup, IsActive, ParentAccountId, CurrencyId)
                SELECT 
                    NEWID(), 
                    TenantId, 
                    Id, 
                    '7000-FX', 
                    'Exchange Gain/Loss', 
                    'Expense', 
                    'Other', 
                    0, 
                    1, 
                    NULL, 
                    NULL 
                FROM Company;

                UPDATE Company SET DefaultExchangeGainLossAccountCode = '7000-FX';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
