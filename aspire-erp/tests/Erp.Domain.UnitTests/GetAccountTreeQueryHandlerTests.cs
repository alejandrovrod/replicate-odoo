using Erp.Application.DTOs;
using Erp.Application.Features.Accounts.Queries;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>Task 2.3: GetAccountTreeQuery assembles a real nested hierarchy from the flat account list.</summary>
public class GetAccountTreeQueryHandlerTests
{
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static Account Account(string code, AccountRootType rootType, bool isGroup, Account? parent = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            AccountCode = code,
            AccountName = code,
            RootType = rootType,
            IsGroup = isGroup,
            ParentAccountId = parent?.Id,
        };

    [Fact]
    public async Task Handle_BuildsNestedTree_WithRootsAndChildren()
    {
        var assets = Account("1000", AccountRootType.Asset, isGroup: true);
        var cash = Account("1110", AccountRootType.Asset, isGroup: false, assets);
        var receivable = Account("1120", AccountRootType.Asset, isGroup: false, assets);
        var expenses = Account("5000", AccountRootType.Expense, isGroup: true);
        var rent = Account("5110", AccountRootType.Expense, isGroup: false, expenses);

        var repository = new FakeAccountRepository
        {
            // Deliberately unordered: the handler must order by AccountCode.
            CompanyAccounts = new[] { rent, receivable, expenses, cash, assets },
        };
        var handler = new GetAccountTreeQueryHandler(repository);

        var tree = await handler.HandleAsync(new GetAccountTreeQuery(CompanyId));

        Assert.Equal(2, tree.Count);
        Assert.Equal("1000", tree[0].Code);
        Assert.Equal("5000", tree[1].Code);

        Assert.Equal(new[] { "1110", "1120" }, tree[0].Children.Select(c => c.Code).ToArray());
        Assert.All(tree[0].Children, child => Assert.False(child.IsGroup));

        var expenseChild = Assert.Single(tree[1].Children);
        Assert.Equal("5110", expenseChild.Code);
        Assert.Empty(expenseChild.Children);
        Assert.True(tree[0].IsActive);
    }

    [Fact]
    public async Task Handle_UnknownCompany_ReturnsEmptyTree()
    {
        var handler = new GetAccountTreeQueryHandler(new FakeAccountRepository());

        var tree = await handler.HandleAsync(new GetAccountTreeQuery(Guid.NewGuid()));

        Assert.Empty(tree);
    }

    [Fact]
    public async Task Handle_AccountWithMissingParent_IsTreatedAsRoot_NeverDropped()
    {
        var orphan = Account("9999", AccountRootType.Liability, isGroup: false);
        orphan.ParentAccountId = Guid.NewGuid(); // parent not in the set (e.g. other company)

        var repository = new FakeAccountRepository { CompanyAccounts = new[] { orphan } };
        var handler = new GetAccountTreeQueryHandler(repository);

        var tree = await handler.HandleAsync(new GetAccountTreeQuery(CompanyId));

        var root = Assert.Single(tree);
        Assert.Equal("9999", root.Code);
    }
}
