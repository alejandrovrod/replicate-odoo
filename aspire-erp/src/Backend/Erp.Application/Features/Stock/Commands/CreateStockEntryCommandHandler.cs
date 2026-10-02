using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Stock.Commands;

/// <summary>
/// Executes <see cref="CreateStockEntryCommand"/> by delegating to <see cref="IStockPostingService"/>
/// and translating its domain failures into <see cref="Result{T}"/> (RFC 7807 <c>code</c>):
/// insufficient stock and validation failures become 4xx in the controller, while configuration
/// faults (missing GL accounts) stay unhandled exceptions on purpose - they are server-side
/// seeding problems, not client errors.
/// </summary>
public sealed class CreateStockEntryCommandHandler : ICommandHandler<CreateStockEntryCommand, Result<StockEntryPostingDto>>
{
    private readonly IStockPostingService _posting;

    public CreateStockEntryCommandHandler(IStockPostingService posting)
    {
        _posting = posting;
    }

    public async Task<Result<StockEntryPostingDto>> HandleAsync(
        CreateStockEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var lines = (command.Lines ?? (IReadOnlyList<CreateStockEntryLine>)Array.Empty<CreateStockEntryLine>())
                .Select(line => new StockPostingLine(line.ItemId, line.Qty, line.Rate))
                .ToList();

            var request = new StockPostingRequest(
                command.CompanyId,
                command.EntryType,
                command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                command.WarehouseId,
                command.TargetWarehouseId,
                lines);

            var result = await _posting.PostAsync(request, cancellationToken);

            return Result<StockEntryPostingDto>.Success(result);
        }
        catch (StockValidationException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            // Task 3.3: the acceptance criterion is this exception reaching the caller as a 4xx.
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            // Only FIFO is implemented in Phase 3 - report it as an explicit client-visible failure.
            return Result<StockEntryPostingDto>.Failure("unsupported_valuation_method", ex.Message);
        }
    }
}
