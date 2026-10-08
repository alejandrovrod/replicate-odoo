using System;
using Erp.Application.Common;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

public record CancelPeriodClosingVoucherCommand(Guid VoucherId) : ICommand<Result<bool>>;
