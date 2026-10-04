using System.Globalization;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Banking.Parsers;

/// <summary>
/// Header-tolerant CSV statement parser (task 6.2). Accepts a date column, a description column
/// and EITHER a single amount column (sign convention: negative amount -&gt; withdrawal) OR a
/// debit/credit column pair (credit -&gt; deposit, debit -&gt; withdrawal). Dependency-free:
/// quote-aware splitting is hand-rolled (Constitution Article I.3).
/// </summary>
public sealed class CsvStatementParser : ICsvStatementParser
{
    private static readonly string[] DateHeaders =
        ["date", "transactiondate", "transaction_date", "posteddate", "posted_date", "valuedate", "value_date"];

    private static readonly string[] DescriptionHeaders =
        ["description", "memo", "narrative", "details", "name"];

    private static readonly string[] AmountHeaders = ["amount"];

    private static readonly string[] DebitHeaders =
        ["debit", "withdrawal", "paidout", "paid_out"];

    private static readonly string[] CreditHeaders =
        ["credit", "deposit", "paidin", "paid_in"];

    public IReadOnlyList<ParsedStatementLine> Parse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new BankingValidationException(
                BankingErrorCodes.EmptyStatement,
                "The statement file is empty: no rows to import.");
        }

        var fileLines = rawContent
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (fileLines.Count < 2)
        {
            throw new BankingValidationException(
                BankingErrorCodes.EmptyStatement,
                "The statement file carries no data rows: a header plus at least one line is required.");
        }

        var headers = SplitRow(fileLines[0]).Select(h => Normalize(h)).ToList();
        var dateIndex = FindColumn(headers, DateHeaders);
        var descriptionIndex = FindColumn(headers, DescriptionHeaders);
        var amountIndex = FindColumn(headers, AmountHeaders);
        var debitIndex = FindColumn(headers, DebitHeaders);
        var creditIndex = FindColumn(headers, CreditHeaders);

        if (dateIndex < 0 || descriptionIndex < 0 || (amountIndex < 0 && (debitIndex < 0 || creditIndex < 0)))
        {
            throw new BankingValidationException(
                BankingErrorCodes.MalformedCsvRow,
                "The statement header must carry a date column, a description column and either "
                + $"an amount column or a debit/credit column pair (line 1: '{fileLines[0]}').");
        }

        var referenceIndex = FindColumn(headers, ["reference", "referencenumber", "reference_number"]);

        var lines = new List<ParsedStatementLine>(fileLines.Count - 1);
        for (var i = 1; i < fileLines.Count; i++)
        {
            lines.Add(ParseRow(fileLines[i], i + 1, dateIndex, descriptionIndex, amountIndex, debitIndex, creditIndex, referenceIndex));
        }

        return lines;
    }

    private static ParsedStatementLine ParseRow(
        string row,
        int lineNumber,
        int dateIndex,
        int descriptionIndex,
        int amountIndex,
        int debitIndex,
        int creditIndex,
        int referenceIndex)
    {
        var cells = SplitRow(row);

        var dateText = Cell(cells, dateIndex);
        if (!TryParseDate(dateText, out var date))
        {
            throw new BankingValidationException(
                BankingErrorCodes.MalformedCsvRow,
                $"Line {lineNumber}: cannot parse date '{dateText}' (expected a calendar date).");
        }

        var description = Cell(cells, descriptionIndex);
        var reference = referenceIndex >= 0 ? Cell(cells, referenceIndex) : string.Empty;

        decimal deposit;
        decimal withdrawal;
        if (amountIndex >= 0)
        {
            var amountText = Cell(cells, amountIndex);
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                throw new BankingValidationException(
                    BankingErrorCodes.MalformedCsvRow,
                    $"Line {lineNumber}: cannot parse amount '{amountText}' (expected a decimal number).");
            }

            deposit = amount >= 0m ? amount : 0m;
            withdrawal = amount < 0m ? -amount : 0m;
        }
        else
        {
            var debitText = Cell(cells, debitIndex);
            var creditText = Cell(cells, creditIndex);
            if (!decimal.TryParse(debitText, NumberStyles.Number, CultureInfo.InvariantCulture, out var debit))
            {
                throw new BankingValidationException(
                    BankingErrorCodes.MalformedCsvRow,
                    $"Line {lineNumber}: cannot parse debit '{debitText}' (expected a decimal number).");
            }

            if (!decimal.TryParse(creditText, NumberStyles.Number, CultureInfo.InvariantCulture, out var credit))
            {
                throw new BankingValidationException(
                    BankingErrorCodes.MalformedCsvRow,
                    $"Line {lineNumber}: cannot parse credit '{creditText}' (expected a decimal number).");
            }

            deposit = credit;
            withdrawal = debit;
        }

        return new ParsedStatementLine(
            lineNumber,
            date,
            deposit,
            withdrawal,
            description,
            string.IsNullOrWhiteSpace(reference) ? null : reference,
            TransactionId: null);
    }

    private static int FindColumn(List<string> headers, string[] candidates)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (candidates.Contains(headers[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Cell(List<string> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index].Trim() : string.Empty;

    private static string Normalize(string header) =>
        header.Trim().ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);

    private static bool TryParseDate(string text, out DateOnly date)
    {
        string[] formats =
        [
            "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "dd/MM/yyyy",
            "dd-MM-yyyy", "MM-dd-yyyy", "yyyyMMdd", "dd.MM.yyyy",
        ];

        return DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Splits one CSV row, honouring double-quoted fields (embedded commas and "" escapes).</summary>
    private static List<string> SplitRow(string row)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < row.Length; i++)
        {
            var c = row[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < row.Length && row[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
