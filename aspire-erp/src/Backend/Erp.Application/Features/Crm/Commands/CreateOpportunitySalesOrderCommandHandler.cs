using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Selling.Commands;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// Executes <see cref="CreateOpportunitySalesOrderCommand"/> (fix-pass C2, spec CRM-02): only a
/// ClosedWon deal converts, the customer resolves from the opportunity's own linkage, and the
/// order itself is built by the EXISTING <see cref="CreateSalesOrderCommandHandler"/> - this
/// handler owns only the CRM-side preconditions, never selling math or numbering.
/// </summary>
/// <remarks>
/// <para><b>Customer resolution.</b> Converted opportunities carry <c>PartyId = Lead.Id</c>
/// (ConvertLead writes the lead, not the customer), so resolution tries the direct customer
/// read first (lead-less deals created with <c>OpportunityFrom = "Customer"</c>) and falls back
/// to the source lead's <c>ConvertedCustomerId</c>. A deal with neither is rejected loudly with
/// <c>crm_opportunity_no_customer</c> - never a silent wrong-customer order.</para>
/// <para><b>Linkage gap (recorded honestly).</b> <see cref="SalesOrder"/> carries NO
/// <c>OpportunityId</c> FK and this pass adds no schema beyond the W1 index, so the linkage is
/// the response itself (order id + number) plus a <c>Note</c> activity on the deal naming the
/// created order number - the spec CRM-03 conversion-note precedent. The note is skipped when
/// no author is attributable (the CRMActivityValidator author rule).</para>
/// </remarks>
public sealed class CreateOpportunitySalesOrderCommandHandler
    : ICommandHandler<CreateOpportunitySalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly ICrmRepository _crmRepository;
    private readonly ICrmActivityRepository _activityRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICommandHandler<CreateSalesOrderCommand, Result<SalesOrderDto>> _createSalesOrder;

    public CreateOpportunitySalesOrderCommandHandler(
        ICrmRepository crmRepository,
        ICrmActivityRepository activityRepository,
        ICustomerRepository customerRepository,
        ICommandHandler<CreateSalesOrderCommand, Result<SalesOrderDto>> createSalesOrder)
    {
        _crmRepository = crmRepository;
        _activityRepository = activityRepository;
        _customerRepository = customerRepository;
        _createSalesOrder = createSalesOrder;
    }

    public async Task<Result<SalesOrderDto>> HandleAsync(
        CreateOpportunitySalesOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opportunity = await _crmRepository.GetOpportunityByIdAsync(
                    command.OpportunityId, cancellationToken)
                ?? throw new CRMValidationException(
                    "crm_opportunity_not_found",
                    $"Opportunity '{command.OpportunityId}' not found.");

            if (opportunity.CompanyId != command.CompanyId)
            {
                throw new CRMValidationException(
                    "crm_opportunity_company_mismatch",
                    "Opportunity does not belong to the specified company.");
            }

            if (opportunity.Status != OpportunityStatus.Won)
            {
                throw new CRMValidationException(
                    CRMErrorCodes.OpportunityNotWon,
                    $"Only a ClosedWon opportunity can create a sales order; "
                    + $"'{opportunity.OpportunityNumber}' is '{opportunity.Status}'.");
            }

            var customer = await ResolveCustomerAsync(opportunity, cancellationToken)
                ?? throw new CRMValidationException(
                    CRMErrorCodes.OpportunityNoCustomer,
                    $"Opportunity '{opportunity.OpportunityNumber}' has no converted customer: "
                    + "convert its source lead first or link the deal to a customer.");

            if (customer.CompanyId != command.CompanyId)
            {
                throw new CRMValidationException(
                    "crm_opportunity_company_mismatch",
                    "Opportunity does not belong to the specified company.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var orderResult = await _createSalesOrder.HandleAsync(
                new CreateSalesOrderCommand(
                    command.CompanyId,
                    customer.Id,
                    command.TransactionDate ?? today,
                    command.DeliveryDate ?? today.AddDays(30),
                    new[] { new CreateSalesOrderLine(command.ItemId, command.Quantity, command.Rate) }),
                cancellationToken);

            if (!orderResult.IsSuccess)
            {
                return orderResult;
            }

            var order = orderResult.Value!;
            var authorId = command.CreatedByUserId ?? opportunity.AssignedSalespersonId;
            if (authorId.HasValue && authorId.Value != Guid.Empty)
            {
                await _activityRepository.AddActivityAsync(new CRMActivity
                {
                    Id = Guid.NewGuid(),
                    OpportunityId = opportunity.Id,
                    Type = CRMActivityType.Note,
                    Subject = $"Sales order {order.OrderNumber} created from this opportunity",
                    Content = $"Sales order {order.OrderNumber} created from opportunity "
                        + $"{opportunity.OpportunityNumber} ({order.GrandTotal:0.00} {opportunity.Currency}). "
                        + "No OpportunityId FK exists on SalesOrder: this note is the linkage.",
                    ActivityDate = DateTimeOffset.UtcNow,
                    CreatedByUserId = authorId.Value,
                }, cancellationToken);
            }

            return Result<SalesOrderDto>.Success(order);
        }
        catch (CRMValidationException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (SalesValidationException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
        catch (CustomerValidationException ex)
        {
            return Result<SalesOrderDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task<Customer?> ResolveCustomerAsync(
        Opportunity opportunity,
        CancellationToken cancellationToken)
    {
        var direct = await _customerRepository.GetByIdAsync(opportunity.PartyId, cancellationToken);
        if (direct is not null)
        {
            return direct;
        }

        var lead = await _crmRepository.GetLeadByIdAsync(opportunity.PartyId, cancellationToken);
        if (lead?.ConvertedCustomerId is { } customerId && customerId != Guid.Empty)
        {
            return await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        }

        return null;
    }
}
