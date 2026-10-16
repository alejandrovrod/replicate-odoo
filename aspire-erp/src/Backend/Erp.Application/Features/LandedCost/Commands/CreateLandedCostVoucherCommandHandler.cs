using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.LandedCost.Commands;

/// <summary>One additional charge on the wire (freight, insurance, customs).</summary>
public sealed record LandedCostChargeInput(
    string Description,
    decimal Amount,
    Guid ExpenseAccountId);

/// <summary>
/// Creates one Draft landed cost voucher with pre-calculated apportionment (Fase 4):
/// validates receipts (Submitted, same company), charges (positive + active-leaf account)
/// and spreads the total over the receipt lines BEFORE anything is posted.
/// </summary>
public sealed record CreateLandedCostVoucherCommand(
    Guid CompanyId,
    DateOnly PostingDate,
    LandedCostDistributeBy DistributeBy,
    IReadOnlyList<Guid> ReceiptIds,
    IReadOnlyList<LandedCostChargeInput>? Charges = null) : ICommand<Result<LandedCostVoucherDto>>;

/// <summary>Submits a Draft voucher: gapless number, inventory GL legs + Kardex revaluation.</summary>
public sealed record SubmitLandedCostVoucherCommand(
    Guid CompanyId,
    Guid VoucherId,
    byte[]? RowVersion = null) : ICommand<Result<LandedCostVoucherDto>>;

/// <summary>Cancels a Submitted voucher via compensating reversal (GL mirror + value negation).</summary>
public sealed record CancelLandedCostVoucherCommand(
    Guid CompanyId,
    Guid VoucherId,
    byte[]? RowVersion = null) : ICommand<Result<LandedCostVoucherDto>>;

public sealed class CreateLandedCostVoucherCommandHandler
    : ICommandHandler<CreateLandedCostVoucherCommand, Result<LandedCostVoucherDto>>
{
    private readonly ILandedCostVoucherRepository _vouchers;
    private readonly IPurchaseRepository _purchases;
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IItemRepository _items;
    private readonly ILandedCostCalculationService _calculation;

    public CreateLandedCostVoucherCommandHandler(
        ILandedCostVoucherRepository vouchers,
        IPurchaseRepository purchases,
        ICompanyRepository companies,
        IAccountRepository accounts,
        IItemRepository items,
        ILandedCostCalculationService? calculation = null)
    {
        _vouchers = vouchers;
        _purchases = purchases;
        _companies = companies;
        _accounts = accounts;
        _items = items;
        _calculation = calculation ?? new LandedCostCalculationService();
    }

    public async Task<Result<LandedCostVoucherDto>> HandleAsync(
        CreateLandedCostVoucherCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new LandedCostValidationException(
                    LandedCostErrorCodes.LandedCostVoucherNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            company.EnsurePostingDateUnlocked(command.PostingDate);
            await _companies.EnsurePostingDateInOpenYearAsync(company.Id, command.PostingDate, cancellationToken);

            var receiptIds = (command.ReceiptIds ?? Array.Empty<Guid>()).ToList();
            LandedCostValidator.EnsureCreatable(receiptIds);

            var charges = command.Charges ?? Array.Empty<LandedCostChargeInput>();
            if (charges.Count == 0 || charges.All(c => c.Amount <= 0m))
            {
                throw new LandedCostValidationException(
                    LandedCostErrorCodes.ChargesRequired,
                    "A landed cost voucher needs at least one charge greater than zero.");
            }

            for (var i = 0; i < charges.Count; i++)
            {
                LandedCostValidator.EnsureValidCharge(
                    charges[i].Description, charges[i].Amount, charges[i].ExpenseAccountId, i);
                await RequirePostingLeafAsync(charges[i].ExpenseAccountId, company.Id, charges[i].Description, cancellationToken);
            }

            // LCV-G4: every receipt must exist, belong to the company and be Submitted.
            var receipts = new List<PurchaseReceipt>(receiptIds.Count);
            foreach (var receiptId in receiptIds)
            {
                var receipt = await _purchases.GetReceiptByIdAsync(receiptId, cancellationToken)
                    ?? throw new LandedCostValidationException(
                        LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                        $"Purchase receipt '{receiptId}' was not found in this tenant.");

                if (receipt.CompanyId != company.Id)
                {
                    throw new LandedCostValidationException(
                        LandedCostErrorCodes.PurchaseReceiptNotSubmitted,
                        $"Purchase receipt '{receipt.VoucherNo}' does not belong to company '{company.Id}'.");
                }

                LandedCostValidator.EnsureSubmittableReceipt(receipt);
                receipts.Add(receipt);
            }

            var inputs = receipts
                .SelectMany(r => r.Lines.Select(l => new LandedCostApportionmentInput(
                    r.Id, l.Id, l.ItemId, l.Qty, l.Amount, l.Rate)))
                .ToList();
            var totalCharges = charges.Sum(c => c.Amount);
            var apportioned = _calculation.Apportion(inputs, totalCharges, command.DistributeBy);

            var voucher = new LandedCostVoucher
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                PostingDate = command.PostingDate,
                Status = DocumentStatus.Draft,
                DistributeBy = command.DistributeBy,
                TotalTaxesAndCharges = totalCharges,
                CreatedAt = DateTimeOffset.UtcNow,
                Receipts = receipts.Select(r => new LandedCostReceiptReference
                {
                    Id = Guid.NewGuid(),
                    PurchaseReceiptId = r.Id,
                }).ToList(),
                Charges = charges.Select(c => new LandedCostChargeItem
                {
                    Id = Guid.NewGuid(),
                    Description = c.Description.Trim(),
                    Amount = c.Amount,
                    ExpenseAccountId = c.ExpenseAccountId,
                }).ToList(),
                Items = apportioned.Select(a => new LandedCostApportionmentItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseReceiptId = a.PurchaseReceiptId,
                    PurchaseReceiptLineId = a.PurchaseReceiptLineId,
                    ItemId = a.ItemId,
                    Qty = a.Qty,
                    OriginalRate = a.OriginalRate,
                    ApplicableCharges = a.ApplicableCharges,
                    NewRate = a.NewRate,
                }).ToList(),
            };

            await _vouchers.AddAsync(voucher, cancellationToken);

            var itemIds = voucher.Items.Select(i => i.ItemId).Distinct().ToList();
            var itemsById = (await _items.GetByIdsAsync(itemIds, cancellationToken))
                .ToDictionary(i => i.Id);
            var receiptNumbers = receipts.ToDictionary(r => r.Id, r => r.VoucherNo);
            return Result<LandedCostVoucherDto>.Success(
                LandedCostVoucherDto.Build(voucher, itemsById, receiptNumbers));
        }
        catch (LandedCostValidationException ex)
        {
            return Result<LandedCostVoucherDto>.Failure(ex.Code, ex.Message);
        }
    }

    private async Task RequirePostingLeafAsync(
        Guid accountId, Guid companyId, string description, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(accountId, ct);
        if (account is null || account.CompanyId != companyId || !account.IsActive || account.IsGroup)
        {
            throw new LandedCostValidationException(
                LandedCostErrorCodes.ExpenseAccountRequired,
                $"Charge '{description}' must reference an active posting (leaf) account of this company.");
        }
    }
}
