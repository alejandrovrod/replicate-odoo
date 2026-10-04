using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Commands;

/// <summary>
/// Executes <see cref="CreateSalesOrderCommand"/>: company/customer/item resolution, pure Domain
/// field validation, server-side totals and the gapless SO number - all inside ONE transaction,
/// because the number is assigned by a SELECT MAX ... WITH (UPDLOCK, HOLDLOCK) that requires it
/// (Constitution III.4). The order is created in Draft (Task 5.2).
/// </summary>
public sealed class CreateSalesOrderCommandHandler
    : ICommandHandler<CreateSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly ICompanyRepository _companies;
    private readonly ICustomerRepository _customers;
    private readonly IItemRepository _items;
    private readonly ISalesOrderRepository _salesOrders;

    public CreateSalesOrderCommandHandler(
        ICompanyRepository companies,
        ICustomerRepository customers,
        IItemRepository items,
        ISalesOrderRepository salesOrders)
    {
        _companies = companies;
        _customers = customers;
        _items = items;
        _salesOrders = salesOrders;
    }

    public async Task<Result<SalesOrderDto>> HandleAsync(
        CreateSalesOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SalesValidator.EnsureHasLines(command.Lines);
            SalesValidator.EnsureValidOrderRequest(
                command.CustomerId, command.TransactionDate, command.DeliveryDate);

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new SalesValidationException(
                    SellingErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            var customer = await _customers.GetByIdAsync(command.CustomerId, cancellationToken)
                ?? throw new SalesValidationException(
                    SellingErrorCodes.CustomerNotFound,
                    $"Customer '{command.CustomerId}' was not found in this tenant.");

            if (customer.CompanyId != company.Id)
            {
                throw new SalesValidationException(
                    SellingErrorCodes.CustomerNotFound,
                    $"Customer '{customer.CustomerCode}' does not belong to company '{company.Id}'.");
            }

            if (!customer.IsActive)
            {
                throw new SalesValidationException(
                    SellingErrorCodes.CustomerInactive,
                    $"Customer '{customer.CustomerCode}' is inactive and cannot receive new sales orders.");
            }

            var transactionDate = command.TransactionDate!.Value;
            var deliveryDate = command.DeliveryDate!.Value;
            var items = await LoadItemsAsync(command.Lines!, cancellationToken);

            // One transaction for number + insert: a rollback releases the lock and consumes no number.
            var order = await _salesOrders.ExecuteInTransactionAsync(async token =>
            {
                var entity = new SalesOrder
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    CustomerId = customer.Id,
                    Status = SalesOrderStatus.Draft,
                    TransactionDate = transactionDate,
                    DeliveryDate = deliveryDate,
                    OrderNumber = await _salesOrders.NextOrderNumberAsync(
                        company.Id, transactionDate.Year, token),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Lines = BuildLines(command.Lines!, items),
                };

                // Totals are computed SERVER-SIDE from the validated lines (the client cannot
                // submit its own money): no tax engine yet, so TaxTotal stays 0.0000 and
                // GrandTotal equals NetTotal until Task 5.3.
                entity.NetTotal = entity.Lines.Sum(l => l.Amount);
                entity.TaxTotal = 0m;
                entity.GrandTotal = entity.NetTotal;
                SalesValidator.EnsureValidTotals(entity.NetTotal, entity.TaxTotal, entity.GrandTotal);

                // Belt and braces: the number must exist BEFORE the insert (plan.md §1 NOT NULL).
                SalesValidator.EnsureOrderNumberAssigned(entity.OrderNumber);

                await _salesOrders.AddOrderAsync(entity, token);
                return entity;
            }, cancellationToken);

            return Result<SalesOrderDto>.Success(SalesOrderDto.Build(order, customer, items));
        }
        catch (SalesValidationException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task<Dictionary<Guid, Item>> LoadItemsAsync(
        IReadOnlyList<CreateSalesOrderLine> lines,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(lines.Count);
        foreach (var line in lines)
        {
            if (line.ItemId == Guid.Empty)
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound, "A line references an empty ItemId.");
            }

            if (!ids.Contains(line.ItemId))
            {
                ids.Add(line.ItemId);
            }
        }

        var found = await _items.GetByIdsAsync(ids, cancellationToken);
        var byId = new Dictionary<Guid, Item>(found.Count);
        foreach (var item in found)
        {
            byId[item.Id] = item;
        }

        foreach (var id in ids)
        {
            if (!byId.ContainsKey(id))
            {
                throw new StockValidationException(
                    StockErrorCodes.ItemNotFound,
                    $"Item '{id}' was not found in this tenant.");
            }
        }

        return byId;
    }

    private static List<SalesOrderItem> BuildLines(
        IReadOnlyList<CreateSalesOrderLine> lines,
        IReadOnlyDictionary<Guid, Item> items)
    {
        var result = new List<SalesOrderItem>(lines.Count);
        foreach (var line in lines)
        {
            // Server-computed amount: Round4(Quantity * Rate), exactly what CK_SalesOrderItem_Amount
            // and the header totals are derived from.
            var amount = Round4(line.Quantity * line.Rate);
            SalesValidator.EnsureValidOrderLine(line.Quantity, line.Rate, amount);
            _ = items[line.ItemId];

            result.Add(new SalesOrderItem
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                DeliveredQuantity = 0m,
                BilledQuantity = 0m,
                Rate = line.Rate,
                Amount = amount,
            });
        }

        return result;
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
