using Erp.Domain.Entities;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Raised when a stock posting needs GL or company configuration that is missing or ambiguous
/// (Constitution III.3 sanity + decision D3): e.g. an item without an ExpenseAccount, a warehouse
/// without a StockAccount, or <c>Company.StockReceivedAccountCode</c> not resolving to exactly one
/// active leaf account. This is a configuration fault, not a user input fault - it must be fixed
/// by seeding/configuration, so it deliberately does NOT map to a 4xx domain failure code.
/// </summary>
public sealed class StockPostingConfigurationException : Exception
{
    public StockPostingConfigurationException(string message) : base(message)
    {
    }
}
