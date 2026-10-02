using Erp.Application.DTOs;
using Erp.Application.Features.Accounts.Commands;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 2.1 / C5: the command handler enforces cycle prevention by walking ancestors through
/// IAccountRepository, plus duplicate-code detection and the wiring of the parent domain rules.
/// </summary>
public class CreateAccountCommandHandlerTests
{
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static Account Group(string code, AccountRootType rootType = AccountRootType.Asset, bool isGroup = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            AccountCode = code,
            AccountName = code,
            RootType = rootType,
            IsGroup = isGroup,
        };

    private static CreateAccountCommand RootCommand(string code = "1000") =>
        new(CompanyId, code, "Assets", AccountRootType.Asset, IsGroup: true, ParentAccountId: null);

    [Fact]
    public async Task Create_RootAccount_Succeeds_AndPersists()
    {
        var repository = new FakeAccountRepository();
        var handler = new CreateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(RootCommand());

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.AddedAccount);
        Assert.Equal(repository.AddedAccount!.Id, result.Value!.Id);
        Assert.Equal("1000", result.Value.Code);
        Assert.Null(result.Value.ParentAccountId);

        // TenantId is stamped by AppDbContext (Constitution II.4) - the handler must not set it.
        Assert.Equal(Guid.Empty, repository.AddedAccount.TenantId);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsDuplicateAccountCodeFailure()
    {
        var repository = new FakeAccountRepository { CodeExists = true };
        var handler = new CreateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(RootCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.DuplicateAccountCode, result.Error!.Code);
        Assert.Null(repository.AddedAccount);
    }

    [Fact]
    public async Task Create_ParentNotFound_ReturnsParentNotFoundFailure()
    {
        var parentId = Guid.NewGuid();
        var repository = new FakeAccountRepository { Ancestors = Array.Empty<Account>() };
        var handler = new CreateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(
            new CreateAccountCommand(CompanyId, "1110", "Cash", AccountRootType.Asset, IsGroup: false, ParentAccountId: parentId));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.ParentNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Create_UnderLeafParent_ReturnsParentIsNotGroupFailure()
    {
        var leaf = Group("1110", isGroup: false);
        var repository = new FakeAccountRepository { Ancestors = new[] { leaf } };
        var handler = new CreateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(
            new CreateAccountCommand(CompanyId, "1111", "Cash", AccountRootType.Asset, IsGroup: false, ParentAccountId: leaf.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.ParentIsNotGroup, result.Error!.Code);
        Assert.Null(repository.AddedAccount);
    }

    [Fact]
    public async Task Create_ChildWithDifferentRootType_ReturnsRootTypeMismatchFailure()
    {
        var parent = Group("1000", AccountRootType.Asset);
        var repository = new FakeAccountRepository { Ancestors = new[] { parent } };
        var handler = new CreateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(
            new CreateAccountCommand(CompanyId, "5110", "Rent", AccountRootType.Expense, IsGroup: false, ParentAccountId: parent.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.RootTypeMismatch, result.Error!.Code);
    }

    [Fact]
    public async Task Create_StoredParentChainLoops_ReturnsCycleDetectedFailure()
    {
        // The fake repository plays the ancestor walk of AccountRepository: a corrupted chain in
        // which the walk returns the same node twice (a stored cycle).
        var parent = Group("1000");
        var repository = new FakeAccountRepository { Ancestors = new[] { parent, parent } };
        var handler = new CreateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(
            new CreateAccountCommand(CompanyId, "1110", "Cash", AccountRootType.Asset, IsGroup: false, ParentAccountId: parent.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.CycleDetected, result.Error!.Code);
        Assert.Null(repository.AddedAccount);
    }

    [Fact]
    public async Task Create_ValidChildUnderGroupParent_Succeeds()
    {
        var parent = Group("1000");
        var repository = new FakeAccountRepository { Ancestors = new[] { parent } };
        var handler = new CreateAccountCommandHandler(repository);

        var result = await handler.HandleAsync(
            new CreateAccountCommand(CompanyId, " 1110 ", "Cash and Bank", AccountRootType.Asset, IsGroup: false, ParentAccountId: parent.Id));

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.AddedAccount);
        Assert.Equal(parent.Id, repository.AddedAccount!.ParentAccountId);
        Assert.Equal("1110", repository.AddedAccount.AccountCode); // trimmed
        Assert.Equal(AccountRootType.Asset, result.Value!.RootType);
    }

    [Fact]
    public async Task Create_InvalidFields_ReturnsFieldFailure()
    {
        var handler = new CreateAccountCommandHandler(new FakeAccountRepository());

        var result = await handler.HandleAsync(new CreateAccountCommand(CompanyId, "", "Cash", AccountRootType.Asset, IsGroup: false, ParentAccountId: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrorCodes.AccountCodeRequired, result.Error!.Code);
    }
}
