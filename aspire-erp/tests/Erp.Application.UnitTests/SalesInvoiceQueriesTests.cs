using Erp.Application.Features.Selling.Queries;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// List/detail reads behind SalesInvoicesController: company-scoped paging,
/// cross-company invisibility (null = 404 at the API) and the empty-page envelope.
/// </summary>
public sealed class SalesInvoiceQueriesTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly FakeSalesInvoiceRepository _salesInvoices = new();

    private static SalesInvoice NewInvoice(Guid companyId, string number)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = companyId,
            InvoiceNumber = number,
            CustomerId = Guid.NewGuid(),
            PostingDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 31),
            Status = SalesInvoiceStatus.Draft,
            NetTotal = 100m,
            TaxTotal = 0m,
            GrandTotal = 100m,
            OutstandingAmount = 100m,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = new List<SalesInvoiceItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ItemId = Guid.NewGuid(),
                    Quantity = 2m,
                    Rate = 50m,
                    Amount = 100m,
                },
            },
        };

    [Fact]
    public async Task GetSalesInvoices_ReturnsOnlyRequestingCompany()
    {
        var mine = NewInvoice(_companyId, "SINV-2026-00001");
        var other = NewInvoice(Guid.NewGuid(), "SINV-2026-00002");
        _salesInvoices.Seed(mine, other);

        var handler = new GetSalesInvoicesQueryHandler(_salesInvoices);
        var page = await handler.HandleAsync(new GetSalesInvoicesQuery(_companyId));

        Assert.Single(page.Items);
        Assert.Equal(mine.Id, page.Items[0].Id);
        Assert.Equal("SINV-2026-00001", page.Items[0].InvoiceNumber);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetSalesInvoices_EmptyCompany_ReturnsEmptyPage()
    {
        var handler = new GetSalesInvoicesQueryHandler(_salesInvoices);
        var page = await handler.HandleAsync(new GetSalesInvoicesQuery(_companyId));

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task GetSalesInvoiceById_ReturnsDtoWhenCompanyMatches()
    {
        var invoice = NewInvoice(_companyId, "SINV-2026-00003");
        _salesInvoices.Seed(invoice);

        var handler = new GetSalesInvoiceByIdQueryHandler(_salesInvoices);
        var dto = await handler.HandleAsync(new GetSalesInvoiceByIdQuery(_companyId, invoice.Id));

        Assert.NotNull(dto);
        Assert.Equal(invoice.Id, dto.Id);
        Assert.Single(dto.Items);
    }

    [Fact]
    public async Task GetSalesInvoiceById_WrongCompany_ReturnsNull()
    {
        var invoice = NewInvoice(_companyId, "SINV-2026-00004");
        _salesInvoices.Seed(invoice);

        var handler = new GetSalesInvoiceByIdQueryHandler(_salesInvoices);
        var dto = await handler.HandleAsync(new GetSalesInvoiceByIdQuery(Guid.NewGuid(), invoice.Id));

        Assert.Null(dto);
    }

    [Fact]
    public async Task GetSalesInvoiceById_Missing_ReturnsNull()
    {
        var handler = new GetSalesInvoiceByIdQueryHandler(_salesInvoices);
        var dto = await handler.HandleAsync(new GetSalesInvoiceByIdQuery(_companyId, Guid.NewGuid()));

        Assert.Null(dto);
    }
}
