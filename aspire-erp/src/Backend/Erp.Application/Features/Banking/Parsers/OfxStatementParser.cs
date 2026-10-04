using System.Globalization;
using System.Text.RegularExpressions;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Banking.Parsers;

/// <summary>
/// OFX 1.x (SGML) statement parser (task 6.2): reads <c>&lt;STMTTRN&gt;</c> blocks and maps
/// <c>TRNTYPE</c> / <c>DTPOSTED</c> / <c>TRNAMT</c> / <c>FITID</c> / <c>NAME</c> / <c>MEMO</c>.
/// Direction follows the SIGN of <c>TRNAMT</c> (negative -&gt; withdrawal). Dependency-free
/// (Constitution Article I.3): SGML tag extraction is regex-based, no OFX package.
/// </summary>
public sealed class OfxStatementParser : IOfxStatementParser
{
    public IReadOnlyList<ParsedStatementLine> Parse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new BankingValidationException(
                BankingErrorCodes.EmptyStatement,
                "The statement file is empty: no rows to import.");
        }

        var fileLines = rawContent.Split(['\r', '\n']);
        var blocks = ExtractBlocks(fileLines);

        if (blocks.Count == 0)
        {
            throw new BankingValidationException(
                BankingErrorCodes.EmptyStatement,
                "The statement file carries no <STMTTRN> blocks: no rows to import.");
        }

        var lines = new List<ParsedStatementLine>(blocks.Count);
        foreach (var (body, lineNumber) in blocks)
        {
            lines.Add(ParseBlock(body, lineNumber));
        }

        return lines;
    }

    private static ParsedStatementLine ParseBlock(string body, int lineNumber)
    {
        var amountText = TagValue(body, "TRNAMT");
        if (amountText is null)
        {
            throw new BankingValidationException(
                BankingErrorCodes.MalformedOfxBlock,
                $"OFX block at line {lineNumber}: missing required <TRNAMT> tag.");
        }

        if (!decimal.TryParse(amountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            throw new BankingValidationException(
                BankingErrorCodes.MalformedOfxBlock,
                $"OFX block at line {lineNumber}: cannot parse <TRNAMT> value '{amountText.Trim()}' "
                + "(expected a decimal number).");
        }

        var postedText = TagValue(body, "DTPOSTED");
        if (postedText is null || !TryParsePostedDate(postedText.Trim(), out var date))
        {
            throw new BankingValidationException(
                BankingErrorCodes.MalformedOfxBlock,
                $"OFX block at line {lineNumber}: cannot parse <DTPOSTED> value '{postedText?.Trim()}' "
                + "(expected YYYYMMDD[HHMMSS]).");
        }

        var name = TagValue(body, "NAME")?.Trim() ?? string.Empty;
        var memo = TagValue(body, "MEMO")?.Trim();
        var description = string.IsNullOrWhiteSpace(memo) ? name : $"{name} {memo}";
        if (string.IsNullOrWhiteSpace(description))
        {
            description = TagValue(body, "TRNTYPE")?.Trim() ?? "OFX transaction";
        }

        var fitid = TagValue(body, "FITID")?.Trim();
        var reference = TagValue(body, "REFNUM")?.Trim() ?? TagValue(body, "CHECKNUM")?.Trim();

        return new ParsedStatementLine(
            lineNumber,
            date,
            Deposit: amount >= 0m ? amount : 0m,
            Withdrawal: amount < 0m ? -amount : 0m,
            description,
            string.IsNullOrWhiteSpace(reference) ? null : reference,
            string.IsNullOrWhiteSpace(fitid) ? null : fitid);
    }

    /// <summary>
    /// Splits the file into (body, 1-based opening line) blocks. An unterminated
    /// <c>&lt;STMTTRN&gt;</c> is malformed, not silently dropped.
    /// </summary>
    private static List<(string Body, int LineNumber)> ExtractBlocks(string[] fileLines)
    {
        var blocks = new List<(string, int)>();
        var open = -1;
        var body = new System.Text.StringBuilder();

        for (var i = 0; i < fileLines.Length; i++)
        {
            var line = fileLines[i];
            if (line.Contains("<STMTTRN>", StringComparison.OrdinalIgnoreCase))
            {
                if (open >= 0)
                {
                    throw new BankingValidationException(
                        BankingErrorCodes.MalformedOfxBlock,
                        $"OFX block at line {open + 1}: unterminated <STMTTRN> (truncated file).");
                }

                open = i;
                body.Clear();
                body.Append(line);
            }
            else if (open >= 0)
            {
                body.Append('\n').Append(line);
                if (line.Contains("</STMTTRN>", StringComparison.OrdinalIgnoreCase))
                {
                    blocks.Add((body.ToString(), open + 1));
                    open = -1;
                }
            }
        }

        if (open >= 0)
        {
            throw new BankingValidationException(
                BankingErrorCodes.MalformedOfxBlock,
                $"OFX block at line {open + 1}: unterminated <STMTTRN> (truncated file).");
        }

        return blocks;
    }

    private static string? TagValue(string body, string tag)
    {
        var match = TagRegex(tag).Match(body);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>OFX 1.x SGML tags are unclosed (<c>&lt;TRNAMT&gt;-12.50</c>): value runs to EOL or next tag.</summary>
    private static Regex TagRegex(string tag) =>
        new($"<{tag}>([^\\r\\n<]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool TryParsePostedDate(string text, out DateOnly date)
    {
        date = default;
        var digits = new string(text.TakeWhile(c => c is >= '0' and <= '9').ToArray());
        return digits.Length >= 8
            && DateOnly.TryParseExact(
                digits[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
