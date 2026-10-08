using System;
using Erp.Application.Common;

namespace Erp.Application.Features.GeneralLedger.PeriodClosing;

public record SubmitPeriodClosingVoucherCommand(Guid VoucherId) : ICommand<Result<bool>>;
