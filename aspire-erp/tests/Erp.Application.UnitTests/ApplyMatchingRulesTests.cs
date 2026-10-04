using Erp.Application.Features.Banking.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 6.3 acceptance through the CQRS handler against in-memory doubles: scenario BN-02
/// (rule -&gt; Matched + pre-populated party/accounts), priority first-win, inactive skipped,
/// account scoping (scoped-first-then-global precedence), auto-create reported-not-posted,
/// and the empty-rule-set zero-change run.
/// </summary>
public sealed class ApplyMatchingRulesTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _otherAccountId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _clearingAccountId = Guid.NewGuid();
    private readonly FakeBankRepository _bank = new();

    public ApplyMatchingRulesTests()
    {
        _bank.SeedAccount(Account(_accountId));
        _bank.SeedAccount(Account(_otherAccountId));
    }

    private BankAccount Account(Guid id) =>
        new()
        {
            Id = id,
            TenantId = _tenantId,
            CompanyId = _companyId,
            AccountName = "Checking",
            BankName = "Acme Bank",
            AccountNumber = id.ToString()[..8],
            GLAccountId = Guid.NewGuid(),
        };

    private BankTransaction StagingLine(string description, decimal deposit, Guid? accountId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            BankAccountId = accountId ?? _accountId,
            TransactionDate = new DateOnly(2026, 10, 2),
            Deposit = deposit,
            Withdrawal = 0m,
            Description = description,
            Status = BankTransactionStatus.Unreconciled,
            RowVersion = new byte[] { 0x01 },
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private BankTransactionRule Rule(
        string name,
        int priority,
        RuleConditionType condition,
        string pattern,
        Guid? accountId = null,
        bool isActive = true,
        bool autoCreate = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyId = _companyId,
            RuleName = name,
            Priority = priority,
            BankAccountId = accountId,
            ConditionType = condition,
            Pattern = pattern,
            TargetPartyType = "Customer",
            TargetPartyId = _customerId,
            AutoCreateVoucher = autoCreate,
            TargetExpenseAccountId = _clearingAccountId,
            IsActive = isActive,
        };

    private ApplyMatchingRulesCommandHandler Handler() => new(_bank);

    // ------------------------------------------------------------- BN-02 happy path

    /// <summary>
    /// Scenario BN-02: the STRIPE line matches the keyword rule -&gt; Matched with the customer
    /// and clearing accounts pre-populated.
    /// </summary>
    [Fact]
    public async Task RunRule_StripeLine_MarksMatchedWithSuggestions()
    {
        var line = StagingLine("STRIPE PAYOUT REF #98234", 5400m);
        _bank.SeedTransaction(line);
        _bank.SeedRule(Rule("Stripe payouts", 1, RuleConditionType.Contains, "STRIPE PAYOUT"));

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.MatchedCount);
        var outcome = Assert.Single(result.Value.Outcomes);
        Assert.True(outcome.Matched);
        Assert.False(outcome.RequiresVoucherCreation);

        Assert.Equal(BankTransactionStatus.Matched, line.Status);
        Assert.Equal("Customer", line.SuggestedPartyType);
        Assert.Equal(_customerId, line.SuggestedPartyId);
        Assert.Equal(_clearingAccountId, line.SuggestedAccountId);
        Assert.Equal(1, _bank.TransactionCount); // ONE transaction
        Assert.Empty(_bank.AddedGlEntries); // no ledger impact from matching
    }

    /// <summary>No rule fires: the line stays Unreconciled with NULL suggestions.</summary>
    [Fact]
    public async Task RunRule_NoMatchingRule_LeavesLineUnreconciled()
    {
        var line = StagingLine("UNKNOWN TRANSFER", 100m);
        _bank.SeedTransaction(line);
        _bank.SeedRule(Rule("Stripe payouts", 1, RuleConditionType.Contains, "STRIPE"));

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.MatchedCount);
        Assert.False(Assert.Single(result.Value.Outcomes).Matched);
        Assert.Equal(BankTransactionStatus.Unreconciled, line.Status);
        Assert.Null(line.SuggestedPartyType);
        Assert.Null(line.SuggestedPartyId);
        Assert.Null(line.SuggestedAccountId);
    }

    // ------------------------------------------------------------- priority / first-win

    /// <summary>Two firing rules: the lower priority number wins and stamps its suggestions.</summary>
    [Fact]
    public async Task RunRule_TwoFiringRules_FirstByPriorityWins()
    {
        var line = StagingLine("STRIPE PAYOUT REF #98234", 5400m);
        _bank.SeedTransaction(line);
        var loser = Rule("Generic payout", 5, RuleConditionType.Contains, "PAYOUT");
        var winner = Rule("Stripe payouts", 1, RuleConditionType.Contains, "STRIPE");
        _bank.SeedRule(loser);
        _bank.SeedRule(winner);

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var outcome = Assert.Single(result.Value!.Outcomes);
        Assert.True(outcome.Matched);
        Assert.Equal(winner.Id, outcome.RuleId);
        Assert.Equal("Stripe payouts", outcome.RuleName);
    }

    /// <summary>Inactive rules never fire, even when their pattern matches.</summary>
    [Fact]
    public async Task RunRule_OnlyInactiveRuleMatches_LeavesLineUnreconciled()
    {
        var line = StagingLine("STRIPE PAYOUT REF #98234", 5400m);
        _bank.SeedTransaction(line);
        _bank.SeedRule(Rule("Retired stripe rule", 1, RuleConditionType.Contains, "STRIPE", isActive: false));

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.MatchedCount);
        Assert.Equal(BankTransactionStatus.Unreconciled, line.Status);
    }

    // ------------------------------------------------------------- account scoping

    /// <summary>
    /// A rule scoped to ANOTHER account never fires on this account's lines, even company-wide.
    /// </summary>
    [Fact]
    public async Task RunRule_ScopedToAnotherAccount_DoesNotFire()
    {
        var line = StagingLine("STRIPE PAYOUT", 100m, _accountId);
        _bank.SeedTransaction(line);
        _bank.SeedRule(Rule(
            "Other account stripe", 1, RuleConditionType.Contains, "STRIPE", accountId: _otherAccountId));

        // Company-wide run: the scoped rule is loaded but must not fire on foreign lines.
        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.MatchedCount);
        Assert.Equal(BankTransactionStatus.Unreconciled, line.Status);
    }

    /// <summary>
    /// Scoped-before-global precedence: both fire, the account-scoped rule wins regardless of
    /// the global rule's better priority number.
    /// </summary>
    [Fact]
    public async Task RunRule_ScopedAndGlobalBothFire_ScopedWins()
    {
        var line = StagingLine("STRIPE PAYOUT", 100m, _accountId);
        _bank.SeedTransaction(line);
        var global = Rule("Global stripe", 1, RuleConditionType.Contains, "STRIPE");
        var scoped = Rule("Account stripe", 99, RuleConditionType.Contains, "STRIPE", accountId: _accountId);
        _bank.SeedRule(global);
        _bank.SeedRule(scoped);

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(scoped.Id, Assert.Single(result.Value!.Outcomes).RuleId);
    }

    /// <summary>Already-Matched lines are NOT re-processed by a later run.</summary>
    [Fact]
    public async Task RunRule_AlreadyMatchedLine_IsSkipped()
    {
        var line = StagingLine("STRIPE PAYOUT", 100m);
        line.Status = BankTransactionStatus.Matched;
        _bank.SeedTransaction(line);
        _bank.SeedRule(Rule("Stripe", 1, RuleConditionType.Contains, "STRIPE"));

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.MatchedCount);
        Assert.Empty(result.Value.Outcomes);
    }

    // ------------------------------------------------------------- auto-create + empty

    /// <summary>
    /// AutoCreateVoucher rules still mark the line Matched with suggestions, report
    /// RequiresVoucherCreation, and post NOTHING (task 6.5 / Block C owns posting).
    /// </summary>
    [Fact]
    public async Task RunRule_AutoCreateRule_ReportsRequiresVoucherCreationWithoutPosting()
    {
        var line = StagingLine("MONTHLY BANK FEE", 0m);
        line.Withdrawal = 15m;
        _bank.SeedTransaction(line);
        _bank.SeedRule(Rule("Bank fees", 1, RuleConditionType.Contains, "BANK FEE", autoCreate: true));

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var outcome = Assert.Single(result.Value!.Outcomes);
        Assert.True(outcome.Matched);
        Assert.True(outcome.RequiresVoucherCreation);
        Assert.Equal(BankTransactionStatus.Matched, line.Status);

        // Reported, not posted: no GL rows, no links, no extra transactions persisted.
        Assert.Empty(_bank.AddedGlEntries);
        Assert.Empty(_bank.Links);
        Assert.Single(_bank.PersistedTransactions);
    }

    /// <summary>Empty rule set: the run succeeds with zero matches and zero mutations.</summary>
    [Fact]
    public async Task RunRule_NoRules_ChangesNothing()
    {
        var line = StagingLine("STRIPE PAYOUT", 100m);
        _bank.SeedTransaction(line);

        var before = (line.Status, line.SuggestedPartyType, line.SuggestedPartyId, line.SuggestedAccountId);

        var result = await Handler().HandleAsync(
            new ApplyMatchingRulesCommand(_companyId, _accountId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.MatchedCount);
        Assert.Equal(before, (line.Status, line.SuggestedPartyType, line.SuggestedPartyId, line.SuggestedAccountId));
    }
}
