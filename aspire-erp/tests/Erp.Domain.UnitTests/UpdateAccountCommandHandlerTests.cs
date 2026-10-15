using Erp.Application.Features.Accounts.Commands;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// COA hardening triad (`.specify/modules/coa-hardening/`): handler-level acceptance for
/// <see cref="UpdateAccountCommandHandler"/> — the Gherkin scenarios COA-G1..G8 that need data
/// access (ancestor walk, children/GL guards, RowVersion gate) and therefore cannot live in the
/// pure <c>AccountValidatorTests</c>.
/// </summary>
public class UpdateAccountCommandHandlerTests
{
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OtherCompanyId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly byte[] RowV1 = [1, 0, 0, 0, 0, 0, 0, 1];
    private static readonly byte[] RowV2 = [2, 0, 0, 0, 0, 0, 0, 2];

    private static Account Stored(
        Guid? id = null,
        Guid? companyId = null,
        string code = "1110",
        string name = "Cash",
        AccountRootType rootType = AccountRootType.Asset,
        bool isGroup = false,
        Guid? parentId = null,
        byte[]? rowVersion = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = TenantId,
            CompanyId = companyId ?? CompanyId,
            AccountCode = code,
            AccountName = name,
            RootType = rootType,
            Type = AccountType.Cash,
            IsGroup = isGroup,
            ParentAccountId = parentId,
            RowVersion = rowVersion ?? (byte[])RowV1.Clone(),
        };

    private static Account Group(string code, AccountRootType rootType = AccountRootType.Asset, Guid? companyId = null) =>
        Stored(companyId: companyId ?? CompanyId, code: code, name: code, rootType: rootType, isGroup: true);

    private static UpdateAccountCommand Update(
        Account stored,
        string name = "Cash Updated",
        bool isActive = true,
        byte[]? rowVersion = null,
        Guid? parentId = null,
        bool changeParent = false,
        bool? isGroup = null,
        Guid? companyId = null) =>
        new(
            stored.Id,
            companyId ?? stored.CompanyId,
            name,
            isActive,
            rowVersion ?? (byte[])stored.RowVersion.Clone(),
            parentId,
            changeParent,
            isGroup);

    // ------------------------------------------------------- COA-G6: RowVersion → 409

    [Fact]
    public async Task Update_MatchingRowVersion_Succeeds_AndKeepsTenantId()
    {
        var stored = Stored();
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored));

        Assert.True(result.IsSuccess);
        Assert.Equal("Cash Updated", result.Value!.Name);
        Assert.Equal(TenantId, stored.TenantId);
    }

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        // Two sessions read v1; session-1 saved (stored is now v2); session-2 saves with v1.
        var stored = Stored(rowVersion: (byte[])RowV2.Clone());
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, rowVersion: (byte[])RowV1.Clone()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
        Assert.Equal("Cash", stored.AccountName); // sin mutación
    }

    [Fact]
    public async Task Update_MissingRowVersion_ReturnsConcurrencyConflict()
    {
        var stored = Stored();
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, rowVersion: []));

        Assert.False(result.IsSuccess);
        Assert.Equal(ConcurrencyErrorCodes.ConcurrencyConflict, result.Error!.Code);
    }

    [Fact]
    public async Task Update_NullName_ReturnsAccountNameRequired_Not500()
    {
        var stored = Stored();
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, name: null!));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.AccountNameRequired, result.Error!.Code);
    }

    // ------------------------------------------------------- not found → 404

    [Fact]
    public async Task Update_UnknownId_ReturnsAccountNotFound()
    {
        var repository = new FakeAccountRepository { AccountById = null };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(
            new UpdateAccountCommand(Guid.NewGuid(), CompanyId, "Cash", true, (byte[])RowV1.Clone()));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.AccountNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Update_CompanyMismatch_ReturnsAccountNotFound()
    {
        var stored = Stored();
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, companyId: OtherCompanyId));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.AccountNotFound, result.Error!.Code);
    }

    // ------------------------------------------------------- COA-G4 / COA-G5: conversión

    [Fact]
    public async Task Update_GroupToLeafWithChildren_ReturnsGroupHasChildren()
    {
        var stored = Stored(isGroup: true);
        var repository = new FakeAccountRepository { AccountById = stored, HasChildren = true };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, isGroup: false));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.GroupHasChildren, result.Error!.Code);
        Assert.True(stored.IsGroup); // el árbol queda intacto
    }

    [Fact]
    public async Task Update_LeafToGroupWithGlEntries_ReturnsAccountHasTransactions()
    {
        var stored = Stored(isGroup: false);
        var repository = new FakeAccountRepository { AccountById = stored, HasGlEntries = true };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, isGroup: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.AccountHasTransactions, result.Error!.Code);
        Assert.False(stored.IsGroup);
    }

    [Fact]
    public async Task Update_ConversionWithoutChildrenOrGl_Succeeds()
    {
        var group = Stored(isGroup: true);
        var repository = new FakeAccountRepository { AccountById = group };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(group, isGroup: false));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsGroup);
    }

    [Fact]
    public async Task Update_NoIsGroupChange_Succeeds_EvenWithChildrenAndGl()
    {
        var stored = Stored(isGroup: false);
        var repository = new FakeAccountRepository
        {
            AccountById = stored,
            HasChildren = true,
            HasGlEntries = true,
        };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored));

        Assert.True(result.IsSuccess);
    }

    // ------------------------------------------------------- COA-G1/G2/G3/G7/G8: reparent

    [Fact]
    public async Task Update_ReparentUnderLeaf_ReturnsParentIsNotGroup()
    {
        var stored = Stored();
        var leaf = Stored(code: "1110");
        var repository = new FakeAccountRepository { AccountById = stored, Ancestors = new[] { leaf } };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, parentId: leaf.Id, changeParent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.ParentIsNotGroup, result.Error!.Code);
        Assert.Null(stored.ParentAccountId);
    }

    [Fact]
    public async Task Update_SelfParent_ReturnsParentIsSelf()
    {
        var stored = Stored(isGroup: true);
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, parentId: stored.Id, changeParent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.ParentIsSelf, result.Error!.Code);
    }

    [Fact]
    public async Task Update_ReparentUnderDescendant_ReturnsCycleDetected()
    {
        // Root → A → B: mover A bajo B hace que el ancestor-walk desde B contenga a A.
        var accountA = Stored(code: "A", isGroup: true);
        var parentB = Stored(code: "B", isGroup: true, parentId: accountA.Id);
        var root = Group("1000");
        var repository = new FakeAccountRepository
        {
            AccountById = accountA,
            Ancestors = new[] { parentB, accountA, root },
        };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(accountA, parentId: parentB.Id, changeParent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.CycleDetected, result.Error!.Code);
    }

    [Fact]
    public async Task Update_ReparentCrossRootType_ReturnsRootTypeMismatch()
    {
        var stored = Stored(rootType: AccountRootType.Expense, isGroup: false);
        var assetParent = Group("1000", AccountRootType.Asset);
        var repository = new FakeAccountRepository { AccountById = stored, Ancestors = new[] { assetParent } };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, parentId: assetParent.Id, changeParent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.RootTypeMismatch, result.Error!.Code);
    }

    [Fact]
    public async Task Update_ReparentToOtherCompany_ReturnsParentNotInSameCompany()
    {
        var stored = Stored();
        var foreignParent = Group("1000", companyId: OtherCompanyId);
        var repository = new FakeAccountRepository { AccountById = stored, Ancestors = new[] { foreignParent } };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, parentId: foreignParent.Id, changeParent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.ParentNotInSameCompany, result.Error!.Code);
    }

    [Fact]
    public async Task Update_ReparentToMissingParent_ReturnsParentNotFound()
    {
        var stored = Stored();
        var repository = new FakeAccountRepository { AccountById = stored, Ancestors = Array.Empty<Account>() };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, parentId: Guid.NewGuid(), changeParent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.ParentNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Update_ValidReparent_Succeeds()
    {
        var stored = Stored();
        var parent = Group("1000");
        var repository = new FakeAccountRepository { AccountById = stored, Ancestors = new[] { parent } };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, parentId: parent.Id, changeParent: true));

        Assert.True(result.IsSuccess);
        Assert.Equal(parent.Id, result.Value!.ParentAccountId);
    }

    [Fact]
    public async Task Update_MoveToRoot_Succeeds()
    {
        var parent = Group("1000");
        var stored = Stored(parentId: parent.Id);
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored, parentId: null, changeParent: true));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.ParentAccountId);
    }

    [Fact]
    public async Task Update_WithoutChangeParent_KeepsParentUntouched()
    {
        var parent = Group("1000");
        var stored = Stored(parentId: parent.Id);
        var repository = new FakeAccountRepository { AccountById = stored };
        var handler = new UpdateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(Update(stored));

        Assert.True(result.IsSuccess);
        Assert.Equal(parent.Id, result.Value!.ParentAccountId);
    }
}
