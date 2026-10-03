using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>One line of a journal entry as returned by the API (account identity included).</summary>
public sealed record JournalEntryLineDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    int LineNumber,
    decimal Debit,
    decimal Credit,
    string? PartyType,
    Guid? PartyId,
    Guid? CostCenterId);

/// <summary>
/// Journal Entry payload (tasks.md 2.3/2.4): header + lines, for the create (Draft), submit,
/// cancel and read endpoints. The response carries NO ledger rows - Task 2.5 owns the GL read
/// model; clients drill into the ledger by VoucherId/VoucherNo.
/// </summary>
/// <param name="RowVersion">
/// Optimistic concurrency token as base64 (SQL Server <c>rowversion</c>). OPTIONAL input of the
/// submit/cancel bodies: a client that wants a compare-and-swap transition echoes the value it
/// read, and a stale value fails with <c>concurrency_conflict</c> (409). Omit it and the
/// server-side token still guards the load/save race - the same protection the purchase voucher
/// submit has (that endpoint simply does not expose the token at all).
/// </param>
public sealed record JournalEntryDto(
    Guid Id,
    Guid CompanyId,
    string VoucherNo,
    DateOnly PostingDate,
    JournalEntryType Type,
    JournalEntryStatus Status,
    string? UserRemark,
    DateTimeOffset CreatedAt,
    string RowVersion,
    IReadOnlyList<JournalEntryLineDto> Lines)
{
    /// <summary>Maps a journal entry aggregate; lines are ordered by <c>LineNumber</c>.</summary>
    public static JournalEntryDto Build(JournalEntry entry)
    {
        var lines = new List<JournalEntryLineDto>(entry.Lines.Count);
        foreach (var line in entry.Lines.OrderBy(l => l.LineNumber))
        {
            lines.Add(new JournalEntryLineDto(
                line.AccountId,
                line.Account?.AccountCode ?? line.AccountId.ToString(),
                line.Account?.AccountName ?? string.Empty,
                line.LineNumber,
                line.Debit,
                line.Credit,
                line.PartyType,
                line.PartyId,
                line.CostCenterId));
        }

        return new JournalEntryDto(
            entry.Id,
            entry.CompanyId,
            entry.VoucherNo,
            entry.PostingDate,
            entry.Type,
            entry.Status,
            entry.UserRemark,
            entry.CreatedAt,
            entry.RowVersion is null || entry.RowVersion.Length == 0
                ? string.Empty
                : Convert.ToBase64String(entry.RowVersion),
            lines);
    }
}
