using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// xunit collection that groups the integration test classes which mutate DATABASE-WIDE state, so
/// xunit runs them one at a time instead of in parallel.
/// </summary>
/// <remarks>
/// <para><b>Why a collection at all:</b> xunit parallelizes test CLASSES (each one lives in its
/// own implicit collection) and serializes only what shares a collection. Two classes in this
/// assembly step on each other's toes when they run side by side:</para>
/// <list type="bullet">
/// <item><see cref="FiscalPeriodLockApiTests"/> asserts a GLOBAL <c>SELECT COUNT_BIG(*) FROM
/// dbo.GLEntry</c> before/after its rejected posting - any other test appending a ledger row in
/// that window fails the assertion;</item>
/// <item>both it and <see cref="JournalEntriesApiTests"/> flip the SHARED
/// <c>Company.FrozenAccountsDate</c> of the dev company (spec AC-04 needs a frozen period), so
/// one class could freeze the books while the other is proving its own posting succeeds.</item>
/// </list>
/// <para>Everything else in the assembly (<see cref="AccountsApiTests"/>,
/// <see cref="GLEntryLedgerGuardTests"/>) touches neither the freeze nor a global ledger count, so
/// it keeps the default parallelism - this collection is the MINIMUM serialization that makes the
/// ledger-mutating tests deterministic.</para>
/// </remarks>
[CollectionDefinition("LedgerMutating")]
public sealed class LedgerMutatingCollection
{
    /// <summary>Collection name shared by every class that writes ledger rows or the freeze date.</summary>
    public const string Name = "LedgerMutating";
}
