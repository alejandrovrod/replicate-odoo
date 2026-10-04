namespace Erp.Application.Features.Banking.Parsers;

/// <summary>Parses CSV bank statements into <see cref="ParsedStatementLine"/> rows (task 6.2).</summary>
/// <remarks>
/// Dependency-free (Constitution Article I.3): header-tolerant (date/description/amount or
/// debit/credit columns) and quote-aware, with no CSV package.
/// </remarks>
public interface ICsvStatementParser
{
    /// <summary>Parses raw CSV content. Throws <c>BankingValidationException</c> on malformed input.</summary>
    IReadOnlyList<ParsedStatementLine> Parse(string rawContent);
}

/// <summary>Parses OFX 1.x (SGML) bank statements into <see cref="ParsedStatementLine"/> rows (task 6.2).</summary>
public interface IOfxStatementParser
{
    /// <summary>Parses raw OFX content. Throws <c>BankingValidationException</c> on malformed input.</summary>
    IReadOnlyList<ParsedStatementLine> Parse(string rawContent);
}
