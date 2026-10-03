using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 2.1 acceptance: unit tests for the Account domain validation logic (pure C#, no EF).
/// Covers the three invariants of Aggregate 2 (.specify/domain_business_rules_ddd.md §3.2) plus
/// the field constraints from plan.md §7.3.
/// </summary>
public class AccountValidatorTests
{
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OtherCompanyId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ParentId = Guid.Parse("30000000-0000-4000-8000-000000000001");

    private static Account NewAccount(
        Guid? id = null,
        Guid? companyId = null,
        string code = "1110",
        string name = "Cash and Bank",
        AccountRootType rootType = AccountRootType.Asset,
        bool isGroup = false,
        Guid? parentId = null,
        string currency = "USD") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            CompanyId = companyId ?? CompanyId,
            AccountCode = code,
            AccountName = name,
            RootType = rootType,
            IsGroup = isGroup,
            ParentAccountId = parentId,
            Currency = currency,
        };

    private static Account NewGroup(
        Guid? id = null,
        Guid? companyId = null,
        string code = "1000",
        string name = "Assets",
        AccountRootType rootType = AccountRootType.Asset) =>
        NewAccount(id, companyId, code, name, rootType, isGroup: true);

    private static string CodeOf(AccountValidationException ex) => ex.Code;

    // ---------------------------------------------------------------- field rules (plan.md §7.3)

    [Fact]
    public void EnsureValidFields_EmptyCompany_ThrowsCompanyRequired()
    {
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(Guid.Empty, "1110", "Cash", AccountRootType.Asset, "USD"));

        Assert.Equal(AccountErrorCodes.CompanyRequired, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidFields_MissingCode_ThrowsAccountCodeRequired()
    {
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(CompanyId, "   ", "Cash", AccountRootType.Asset, "USD"));

        Assert.Equal(AccountErrorCodes.AccountCodeRequired, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidFields_CodeOver50Chars_ThrowsAccountCodeTooLong()
    {
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(CompanyId, new string('9', 51), "Cash", AccountRootType.Asset, "USD"));

        Assert.Equal(AccountErrorCodes.AccountCodeTooLong, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidFields_MissingName_ThrowsAccountNameRequired()
    {
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(CompanyId, "1110", null, AccountRootType.Asset, "USD"));

        Assert.Equal(AccountErrorCodes.AccountNameRequired, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidFields_NameOver150Chars_ThrowsAccountNameTooLong()
    {
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(CompanyId, "1110", new string('n', 151), AccountRootType.Asset, "USD"));

        Assert.Equal(AccountErrorCodes.AccountNameTooLong, CodeOf(ex));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    public void EnsureValidFields_UndefinedRootType_ThrowsInvalidRootType(int rawValue)
    {
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(CompanyId, "1110", "Cash", (AccountRootType)rawValue, "USD"));

        Assert.Equal(AccountErrorCodes.InvalidRootType, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidFields_CurrencyOver3Chars_ThrowsCurrencyInvalid()
    {
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(CompanyId, "1110", "Cash", AccountRootType.Asset, "USDX"));

        Assert.Equal(AccountErrorCodes.CurrencyInvalid, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidFields_ValidAccount_DoesNotThrow()
    {
        AccountValidator.EnsureValidFields(CompanyId, "1110", "Cash and Bank", AccountRootType.Asset, "USD");
    }

    // -------------------------------------------- account type rules (plan.md §7.3 Type, Task 1.1)

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    public void EnsureValidFields_UndefinedAccountType_ThrowsInvalidAccountType(int rawValue)
    {
        // Type is persisted as a NAME string (NVARCHAR(50)); an out-of-range cast (e.g. a JSON
        // number outside the enum) must fail as a domain rule instead of writing garbage.
        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureValidFields(CompanyId, "1110", "Cash", AccountRootType.Asset, "USD", (AccountType)rawValue));

        Assert.Equal(AccountErrorCodes.InvalidAccountType, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidFields_ValidAccountType_DoesNotThrow()
    {
        AccountValidator.EnsureValidFields(CompanyId, "1110", "Cash", AccountRootType.Asset, "USD", AccountType.Cash);
    }

    [Fact]
    public void EnsureValidFields_OmittedAccountType_DefaultsToOther()
    {
        // Backward compatibility (Task 1.1): the parameter is optional, so callers written before
        // the Type column existed validate the entity-level default (AccountType.Other).
        AccountValidator.EnsureValidFields(CompanyId, "1110", "Cash", AccountRootType.Asset, "USD");
    }

    [Fact]
    public void AccountType_OtherIsValueZero_MatchesEntityAndDatabaseDefault()
    {
        // AccountType.Other must stay the first member: it is the CLR default of the property
        // (EF "not set" sentinel), the Account.Type initializer, the SQL DEFAULT of the column
        // and the Asset/Liability per-RootType default - all must converge on 'Other'.
        Assert.Equal(0, (int)AccountType.Other);
        Assert.Equal(AccountType.Other, default(AccountType));
    }

    [Fact]
    public void AccountType_AllNamesFitInTheFiftyCharacterColumn()
    {
        // plan.md §7.3: Type NVARCHAR(50) - the NAME conversion cannot store longer members.
        foreach (var name in Enum.GetNames<AccountType>())
        {
            Assert.True(name.Length <= 50, $"AccountType member '{name}' exceeds 50 characters.");
        }
    }

    // ------------------------------------------------------- parent rules (invariants §3.2.1/3.2.2)

    [Fact]
    public void EnsureValidParent_LeafParent_ThrowsParentIsNotGroup()
    {
        // Ubiquitous language: a Leaf / Posting Account is terminal - only Group Accounts take children.
        var leaf = NewAccount(code: "1110", isGroup: false);
        var child = NewAccount(code: "1111", parentId: leaf.Id);

        var ex = Assert.Throws<AccountValidationException>(() => AccountValidator.EnsureValidParent(child, leaf));

        Assert.Equal(AccountErrorCodes.ParentIsNotGroup, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidParent_ParentInAnotherCompany_ThrowsParentNotInSameCompany()
    {
        var parent = NewGroup(companyId: OtherCompanyId);
        var child = NewAccount(parentId: parent.Id);

        var ex = Assert.Throws<AccountValidationException>(() => AccountValidator.EnsureValidParent(child, parent));

        Assert.Equal(AccountErrorCodes.ParentNotInSameCompany, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidParent_RootTypeDiffersFromParent_ThrowsRootTypeMismatch()
    {
        // Business rule 3.2.2: a child account must inherit the parent's RootType.
        var parent = NewGroup(rootType: AccountRootType.Asset);
        var child = NewAccount(rootType: AccountRootType.Expense, parentId: parent.Id);

        var ex = Assert.Throws<AccountValidationException>(() => AccountValidator.EnsureValidParent(child, parent));

        Assert.Equal(AccountErrorCodes.RootTypeMismatch, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidParent_ParentIsTheAccountItself_ThrowsParentIsSelf()
    {
        var account = NewAccount(code: "1000", isGroup: true);
        account.ParentAccountId = account.Id;

        var ex = Assert.Throws<AccountValidationException>(() => AccountValidator.EnsureValidParent(account, account));

        Assert.Equal(AccountErrorCodes.ParentIsSelf, CodeOf(ex));
    }

    [Fact]
    public void EnsureValidParent_GroupParentSameCompanySameRootType_DoesNotThrow()
    {
        var parent = NewGroup();
        var child = NewAccount(rootType: AccountRootType.Asset, parentId: parent.Id);

        AccountValidator.EnsureValidParent(child, parent);
    }

    // -------------------------------------------------- cycle prevention (walked by the handler)

    [Fact]
    public void EnsureNoCycle_AncestorChainContainsCandidate_ThrowsCycleDetected()
    {
        var candidateId = Guid.NewGuid();
        var grandParentId = Guid.NewGuid();

        // candidate -> parent -> grandparent, and the stored grandparent IS the candidate.
        var chain = new List<Guid> { Guid.NewGuid(), grandParentId, candidateId };

        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureNoCycle(candidateId, chain));

        Assert.Equal(AccountErrorCodes.CycleDetected, CodeOf(ex));
    }

    [Fact]
    public void EnsureNoCycle_ChainWithRepeatedId_ThrowsCycleDetected()
    {
        var parentId = Guid.NewGuid();
        var chain = new List<Guid> { parentId, Guid.NewGuid(), parentId };

        var ex = Assert.Throws<AccountValidationException>(() =>
            AccountValidator.EnsureNoCycle(Guid.NewGuid(), chain));

        Assert.Equal(AccountErrorCodes.CycleDetected, CodeOf(ex));
    }

    [Fact]
    public void EnsureNoCycle_LinearChain_DoesNotThrow()
    {
        var chain = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        AccountValidator.EnsureNoCycle(Guid.NewGuid(), chain);
    }

    [Fact]
    public void EnsureNoCycle_EmptyChain_DoesNotThrow()
    {
        AccountValidator.EnsureNoCycle(Guid.NewGuid(), Array.Empty<Guid>());
    }
}
