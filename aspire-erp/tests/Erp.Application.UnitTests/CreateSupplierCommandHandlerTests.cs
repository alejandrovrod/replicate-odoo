using Erp.Application.Features.Buying.Commands;
using Erp.Application.UnitTests.Fakes;
using Erp.Domain.Entities;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Task 4.1 acceptance ("Suppliers validate unique CODE per tenant") exercised through the CQRS
/// handler: pure Domain validation, trimming and the duplicate-code rule surfacing as a domain
/// failure the API maps to 409 (mirrors CreateItemCommandHandlerTests).
/// </summary>
public sealed class CreateSupplierCommandHandlerTests
{
    private readonly FakeSupplierRepository _suppliers = new();

    private CreateSupplierCommandHandler CreateHandler() => new(_suppliers);

    [Fact]
    public async Task HandleAsync_ValidSupplier_PersistsTrimmedSupplierAndReturnsDto()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("  SUP-001  ", " Acme Industrial Supplies "));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SUP-001", result.Value!.Code); // trimmed
        Assert.Equal("Acme Industrial Supplies", result.Value.Name);
        Assert.True(result.Value.IsActive);

        var saved = _suppliers.AddedSupplier;
        Assert.NotNull(saved);
        Assert.Equal("SUP-001", saved!.Code);
        Assert.Equal(Guid.Empty, saved.TenantId); // stamped by AppDbContext, never by the handler
    }

    [Fact]
    public async Task HandleAsync_DuplicateCodeInTenant_FailsWithDuplicateSupplierCode()
    {
        _suppliers.Seed(new Supplier
        {
            Id = Guid.NewGuid(),
            Code = "SUP-001",
            Name = "First Vendor",
        });

        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("SUP-001", "Second Vendor"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PurchaseErrorCodes.DuplicateSupplierCode, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_MissingCode_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("   ", "Acme"));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierCodeRequired, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_MissingName_Fails()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("SUP-001", ""));

        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseErrorCodes.SupplierNameRequired, result.Error!.Code);
        Assert.Null(_suppliers.AddedSupplier);
    }

    [Fact]
    public async Task HandleAsync_InactiveSupplier_CanStillBeCreated()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSupplierCommand("SUP-002", "Blocked Vendor", IsActive: false));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
        Assert.False(_suppliers.AddedSupplier!.IsActive);
    }
}

