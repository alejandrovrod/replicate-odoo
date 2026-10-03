using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.GeneralLedger.Commands;

/// <summary>
/// Executes <see cref="CreateJournalEntryCommand"/>: company resolution, pure Domain line
/// validation and the gapless JV voucher - all inside ONE transaction, because the number is
/// assigned by a SELECT MAX ... WITH (UPDLOCK, HOLDLOCK) that requires it
/// (Constitution III.4). The entry is created in <see cref="JournalEntryStatus.Draft"/> and
/// writes ZERO GLEntry rows (the posting happens in SubmitJournalEntryCommandHandler).
/// </summary>
public sealed class CreateJournalEntryCommandHandler
    : ICommandHandler<CreateJournalEntryCommand, Result<JournalEntryDto>>
{
    /// <summary>Voucher prefix of spec AC-07's literal <c>JV-2026-0081</c>.</summary>
    private const string VoucherPrefix = "JV";

    private readonly ICompanyRepository _companies;
    private readonly IJournalRepository _journals;

    public CreateJournalEntryCommandHandler(
        ICompanyRepository companies,
        IJournalRepository journals)
    {
        _companies = companies;
        _journals = journals;
    }

    public async Task<Result<JournalEntryDto>> HandleAsync(
        CreateJournalEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            JournalEntryValidator.EnsureHasLines(command.Lines);

            var company = await _companies.GetByIdAsync(command.CompanyId, cancellationToken)
                ?? throw new JournalValidationException(
                    JournalErrorCodes.CompanyNotFound,
                    $"Company '{command.CompanyId}' was not found in this tenant.");

            var postingDate = command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // Structure only (see the command remarks): every line must carry a legal amount so
            // the CHECK constraints can never be the thing that rejects a saved draft.
            var lines = new List<JournalEntryLine>(command.Lines!.Count);
            for (var i = 0; i < command.Lines.Count; i++)
            {
                var line = command.Lines[i];
                JournalEntryValidator.EnsureValidLine(line.Debit, line.Credit);

                lines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountId = line.AccountId,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    PartyType = line.PartyType,
                    PartyId = line.PartyId,
                    CostCenterId = line.CostCenterId,
                    LineNumber = i + 1,
                });
            }

            // One transaction for number + insert: a rollback releases the lock and consumes no
            // number (Constitution III.4).
            var entry = await _journals.ExecuteInTransactionAsync(async token =>
            {
                var entity = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Status = JournalEntryStatus.Draft,
                    Type = command.Type,
                    PostingDate = postingDate,
                    UserRemark = command.UserRemark,
                    VoucherNo = await _journals.NextVoucherNumberAsync(
                        company.Id, VoucherPrefix, postingDate.Year, token),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Lines = lines,
                };

                await _journals.AddAsync(entity, token);
                return entity;
            }, cancellationToken);

            return Result<JournalEntryDto>.Success(JournalEntryDto.Build(entry));
        }
        catch (JournalValidationException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Code, ex.Message);
        }
    }
}
