using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Executes <see cref="CompleteManufactureCommand"/> by delegating to
/// <see cref="IManufacturingPostingService"/> and translating its domain failures into
/// <see cref="Result{T}"/> (RFC 7807 <c>code</c>): insufficient WIP and validation failures
/// become 4xx in the controller, while configuration faults (missing GL accounts) stay
/// unhandled exceptions on purpose - they are server-side seeding problems, not client errors.
/// </summary>
public sealed class CompleteManufactureCommandHandler
    : ICommandHandler<CompleteManufactureCommand, Result<StockEntryPostingDto>>
{
    private readonly IManufacturingPostingService _posting;

    public CompleteManufactureCommandHandler(IManufacturingPostingService posting)
    {
        _posting = posting;
    }

    public async Task<Result<StockEntryPostingDto>> HandleAsync(
        CompleteManufactureCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _posting.CompleteAsync(
                new WorkOrderCompletionRequest(
                    command.CompanyId,
                    command.WorkOrderId,
                    command.ProducedQuantity,
                    command.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow)),
                cancellationToken);

            return Result<StockEntryPostingDto>.Success(result);
        }
        catch (ManufacturingValidationException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (StockValidationException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            // Task MF-06: WIP cannot cover the consumption - the order stays InProcess, untouched.
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<StockEntryPostingDto>.Failure(ex.Code, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            // Only FIFO is implemented - report it as an explicit client-visible failure.
            return Result<StockEntryPostingDto>.Failure("unsupported_valuation_method", ex.Message);
        }
    }
}
