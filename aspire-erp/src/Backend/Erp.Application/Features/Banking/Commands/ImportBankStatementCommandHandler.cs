using Erp.Application.Common;
using Erp.Application.Features.Banking.Parsers;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Commands;

public sealed class ImportBankStatementCommandHandler
    : ICommandHandler<ImportBankStatementCommand, Result<BankStatementImportSummary>>
{
    private readonly IBankRepository _bank;
    private readonly ICsvStatementParser _csv;
    private readonly IOfxStatementParser _ofx;

    public ImportBankStatementCommandHandler(
        IBankRepository bank,
        ICsvStatementParser csv,
        IOfxStatementParser ofx)
    {
        _bank = bank;
        _csv = csv;
        _ofx = ofx;
    }

    public async Task<Result<BankStatementImportSummary>> HandleAsync(
        ImportBankStatementCommand request,
        CancellationToken cancellationToken)
    {
        var account = await _bank.GetAccountByIdAsync(request.BankAccountId, cancellationToken);
        if (account is null || account.CompanyId != request.CompanyId)
        {
            return Result<BankStatementImportSummary>.Failure(
                BankingErrorCodes.BankAccountNotFound,
                $"Bank account '{request.BankAccountId}' not found in company '{request.CompanyId}'.");
        }

        IReadOnlyList<ParsedStatementLine> parsed;
        try
        {
            parsed = request.Format.Trim().ToUpperInvariant() switch
            {
                "CSV" => _csv.Parse(request.RawContent),
                "OFX" => _ofx.Parse(request.RawContent),
                _ => throw new BankingValidationException(
                    BankingErrorCodes.InvalidStatementFormat,
                    $"Unsupported statement format '{request.Format}' (expected 'CSV' or 'OFX')."),
            };
        }
        catch (BankingValidationException ex)
        {
            return Result<BankStatementImportSummary>.Failure(ex.Code, ex.Message);
        }

        try
        {
            foreach (var line in parsed)
            {
                BankTransactionValidator.EnsureValidSides(line.Deposit, line.Withdrawal);
            }
        }
        catch (BankingValidationException ex)
        {
            return Result<BankStatementImportSummary>.Failure(ex.Code, ex.Message);
        }

        var wantedIds = parsed
            .Select(l => l.TransactionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var alreadyImported = wantedIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(
                await _bank.GetImportedTransactionIdsAsync(account.Id, wantedIds, cancellationToken),
                StringComparer.Ordinal);

        var seenInBatch = new HashSet<string>(StringComparer.Ordinal);
        var fresh = new List<ParsedStatementLine>(parsed.Count);
        var duplicates = 0;
        foreach (var line in parsed)
        {
            if (line.TransactionId is not null
                && (alreadyImported.Contains(line.TransactionId) || !seenInBatch.Add(line.TransactionId)))
            {
                duplicates++;
                continue;
            }

            fresh.Add(line);
        }

        return await _bank.ExecuteInTransactionAsync(async token =>
        {
            var batch = new BankStatementImport
            {
                Id = Guid.NewGuid(),
                TenantId = account.TenantId,
                CompanyId = request.CompanyId,
                BankAccountId = account.Id,
                FileName = request.FileName,
                ImportDate = DateTimeOffset.UtcNow,
                TotalTransactionsCount = parsed.Count,
                ImportedCount = fresh.Count,
                DuplicateCount = duplicates,
                ImportStatus = BankImportStatus.Processed,
            };

            await _bank.AddImportAsync(batch, token);

            var rows = fresh.Select(line => new BankTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = account.TenantId,
                CompanyId = request.CompanyId,
                BankAccountId = account.Id,
                TransactionDate = line.TransactionDate,
                Deposit = line.Deposit,
                Withdrawal = line.Withdrawal,
                Currency = string.IsNullOrWhiteSpace(line.Currency) ? "USD" : line.Currency.Trim(),
                Description = line.Description,
                ReferenceNumber = line.ReferenceNumber,
                TransactionId = line.TransactionId,
                TransactionType = line.TransactionType ?? string.Empty,
                Status = BankTransactionStatus.Unreconciled,
                // Staging isolation (BN-01): import writes staging rows only, never GLEntry.
                // The line arrives fully unallocated and unevaluated by the rules engine.
                UnallocatedAmount = line.Deposit - line.Withdrawal,
                IsRuleEvaluated = false,
                CreatedAt = DateTimeOffset.UtcNow,
            }).ToList();

            await _bank.AddTransactionsAsync(rows, token);

            return Result<BankStatementImportSummary>.Success(new BankStatementImportSummary(
                batch.Id,
                batch.FileName,
                batch.TotalTransactionsCount,
                batch.ImportedCount,
                batch.DuplicateCount));
        }, cancellationToken);
    }
}
