using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IJournalRepository"/>: gapless JV vouchers come from a per-(prefix, year)
/// sequence, the posting transaction executes its callback (and rolls the captured writes back
/// when it throws, mirroring the real transaction), and every persisted header / appended GLEntry
/// row is captured so the handlers' "zero ledger rows on rejection" promise (spec AC-02) can be
/// asserted without a database.
/// </summary>
public sealed class FakeJournalRepository : IJournalRepository
{
    private readonly List<JournalEntry> _entries = new();
    private readonly List<GLEntry> _glEntries = new();
    private readonly Dictionary<(string Prefix, int Year), int> _sequences = new();

    public IReadOnlyList<JournalEntry> Entries => _entries;

    /// <summary>Every GLEntry row the handlers appended - the AC-02 "zero records" assertions read this.</summary>
    public IReadOnlyList<GLEntry> GlEntries => _glEntries;

    /// <summary>Number of transactions opened (proves each workflow step runs inside ONE transaction).</summary>
    public int TransactionCount { get; private set; }

    /// <summary>Pre-loads an entry (submit/cancel tests start from Draft/Submitted rows directly).</summary>
    public void Seed(params JournalEntry[] entries) => _entries.AddRange(entries);

    /// <summary>
    /// When set, the NEXT <c>UpdateAsync</c> fails like the real repository does after a
    /// RowVersion mismatch (<c>DbUpdateConcurrencyException</c> translated to
    /// <see cref="ConcurrencyConflictException"/>); the flag resets itself so only one call fails.
    /// </summary>
    public bool FailNextUpdate { get; set; }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCount++;

        // Snapshot everything a handler can write so a throwing callback rolls back like the
        // real transaction - that is what makes "rejected submit = zero GLEntry rows" testable.
        var entriesBefore = _entries.Count;
        var glBefore = _glEntries.Count;
        var sequencesBefore = new Dictionary<(string, int), int>(_sequences);

        try
        {
            return operation(cancellationToken);
        }
        catch
        {
            if (_glEntries.Count > glBefore)
            {
                _glEntries.RemoveRange(glBefore, _glEntries.Count - glBefore);
            }

            if (_entries.Count > entriesBefore)
            {
                _entries.RemoveRange(entriesBefore, _entries.Count - entriesBefore);
            }

            _sequences.Clear();
            foreach (var pair in sequencesBefore)
            {
                _sequences[pair.Key] = pair.Value;
            }

            throw;
        }
    }

    public Task<string> NextVoucherNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
    {
        var key = (prefix, year);
        _sequences.TryGetValue(key, out var current);
        _sequences[key] = current + 1;
        return Task.FromResult($"{prefix}-{year}-{current + 1:D5}");
    }

    public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        if (FailNextUpdate)
        {
            FailNextUpdate = false;
            throw new ConcurrencyConflictException(nameof(JournalEntry), entry.Id);
        }

        // In-memory: the entity instance IS the store; the workflow mutation already happened.
        return Task.CompletedTask;
    }

    public Task AddGlEntriesAsync(
        IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _glEntries.AddRange(glEntries);
        return Task.CompletedTask;
    }

    public Task<JournalEntry?> GetByIdAsync(
        Guid journalEntryId, CancellationToken cancellationToken = default)
        => Task.FromResult(_entries.FirstOrDefault(e => e.Id == journalEntryId));

    public Task<IReadOnlyList<JournalEntry>> GetRecentByCompanyAsync(
        Guid companyId, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<JournalEntry>>(
            _entries
                .Where(e => e.CompanyId == companyId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToList());
}

