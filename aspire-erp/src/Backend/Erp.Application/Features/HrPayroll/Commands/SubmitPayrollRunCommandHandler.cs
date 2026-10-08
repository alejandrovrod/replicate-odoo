using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;

namespace Erp.Application.Features.HrPayroll.Commands;

/// <summary>
/// Executes <see cref="SubmitPayrollRunCommand"/> (Task 12.3, spec HR-01/HR-02/HR-03/HR-04/HR-06):
/// period/company resolution, the frozen-period gate, eligibility (HR-03), per-employee pricing
/// through the pure <see cref="PayrollCalculator"/>, replay-safe slip inserts, the per-component
/// accrual voucher (HR-02) and the Draft -&gt; Submitted transition - all inside ONE
/// <c>IHrPayrollRepository</c> transaction, so a rejected run writes zero rows.
/// </summary>
/// <remarks>
/// <para>
/// Accrual shape (DESIGN DECISION, reconciled with the 12.2 leaf mapping): one Dr line per
/// distinct earning GL account (summed across slips), one Cr line per distinct deduction GL
/// account, plus ONE Cr PayrollPayable line for TotalNetPay. With the spec's standard structure
/// (all earnings -&gt; 5110, tax -&gt; 2220, pension -&gt; 2225) this yields EXACTLY the HR-02
/// literals (Dr 5110 / Cr 2220 / Cr 2225 / Cr 2150). Zero-amount component lines are omitted;
/// the payable line always posts (one-voucher-per-run, the depreciation precedent).
/// </para>
/// <para>
/// Skip-vs-fail verdicts (documented, deliberate - the depreciation skip-and-report precedent):
/// ineligible employees (HR-03), missing/inactive structures and inactive/missing components are
/// data states, not operator errors - each is SKIPPED with its identity + reason and reported.
/// A run that prices ZERO slips fails loud (<c>no_eligible_employees</c>); a zero-line voucher
/// is never posted. An already-present (entry, employee) slip is SKIPPED with reason
/// (replay-safe); the repository guard underneath still throws the typed
/// <c>duplicate_salary_slip</c> conflict for genuine races (HR-06).
/// </para>
/// <para>
/// Pricing reuses the Block A calculator WITHOUT reimplementing its math: the percentage base
/// is the sum of fixed earning amounts, each line is priced through
/// <see cref="PayrollCalculator.CalculateStructureTotals"/> (single-line input), prorated lines
/// scale by PaymentDays / 30 AFTER the calculator returns, and the final identity + floor comes
/// from <see cref="PayrollCalculator.CalculateSalarySlip"/>. Deduction surplus past zero net is
/// ABSORBED (Block A boundary - no carry-forward).
/// </para>
/// </remarks>
public sealed class SubmitPayrollRunCommandHandler
    : ICommandHandler<SubmitPayrollRunCommand, Result<PayrollSubmitResultDto>>
{
    private const string VoucherType = "Payroll";
    private const string EntryPrefix = "PE";
    private const string VoucherPrefix = "PYR";

    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly IHrPayrollRepository _hr;

    public SubmitPayrollRunCommandHandler(
        ICompanyRepository companies,
        IAccountRepository accounts,
        IHrPayrollRepository hr)
    {
        _companies = companies;
        _accounts = accounts;
        _hr = hr;
    }

    public async Task<Result<PayrollSubmitResultDto>> HandleAsync(
        SubmitPayrollRunCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _hr.ExecuteInTransactionAsync(async token =>
            {
                PayrollValidator.EnsureValidPeriod(command.StartDate, command.EndDate);

                var company = await _companies.GetByIdAsync(command.CompanyId, token)
                    ?? throw new HrValidationException(
                        HrPayrollErrorCodes.CompanyNotFound,
                        $"Company '{command.CompanyId}' was not found in this tenant.");

                // tasks.md 2.2 / spec AC-04: hard fiscal period lock - BEFORE a single slip or
                // GLEntry line is built, so a back-dated run modifies ZERO data.
                company.EnsurePostingDateUnlocked(command.PostingDate);

                var overrides = ValidateOverrides(command.PaymentDayOverrides);

                var employees = await LoadAllAsync(
                    (paging, t) => _hr.GetEmployeesByCompanyAsync(command.CompanyId, paging, t),
                    token);
                var assignments = await LoadAllAsync(
                    (paging, t) => _hr.GetAssignmentsByCompanyAsync(command.CompanyId, paging, t),
                    token);
                var components = await LoadAllAsync(
                    (paging, t) => _hr.GetComponentsByCompanyAsync(command.CompanyId, paging, t),
                    token);
                var componentsById = components.ToDictionary(c => c.Id);

                var assignmentsByEmployee = assignments
                    .Where(a => a.IsActive)
                    .GroupBy(a => a.EmployeeId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<SalaryStructureAssignment>)g.ToList());

                // Reserve the entry first (Draft, number assigned inside the transaction -
                // Constitution III.4, the work-order precedent). Slips attach to it below.
                var payrollNumber = await _hr.NextPayrollNumberAsync(company.Id, command.PostingDate.Year, token);

                // Block C overlap guard (HR-06 live provability): a second submit for an
                // overlapping period would double-pay (separate entries, no shared slip rows -
                // the (PayrollEntryId, EmployeeId) unique index cannot catch it). Runs AFTER
                // NextPayrollNumberAsync took its UPDLOCK/HOLDLOCK over the company's year
                // range (held to transaction end) but BEFORE the entry row is inserted, so
                // concurrent same-year submits serialize on the numbering lock and the loser
                // observes the winner's committed entry here (409, zero writes). Cancelled
                // runs released their period (mirror posted), so they never block a re-run.
                if (await _hr.HasOverlappingEntryAsync(command.CompanyId, command.StartDate, command.EndDate, token))
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.PayrollPeriodOverlap,
                        $"Company '{command.CompanyId}' already has a non-Cancelled payroll run overlapping "
                        + $"[{command.StartDate:yyyy-MM-dd}..{command.EndDate:yyyy-MM-dd}]. "
                        + "Cancel that run before submitting an overlapping period.");
                }

                var entry = new PayrollEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    PayrollNumber = payrollNumber,
                    StartDate = command.StartDate,
                    EndDate = command.EndDate,
                    PostingDate = command.PostingDate,
                    Status = PayrollEntryStatus.Draft,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                await _hr.AddPayrollEntryAsync(entry, token);

                // HR-06 live race guard: hold the entry's slip range to transaction end so
                // concurrent inserts for this entry serialize; the unique index stays authoritative.
                await _hr.LockEntrySlipsAsync(entry.Id, token);

                var skipped = new List<PayrollSkipDto>();
                var priced = new List<PricedSlip>();

                foreach (var employee in employees
                    .Where(e => e.CompanyId == command.CompanyId)
                    .OrderBy(e => e.EmployeeNumber, StringComparer.Ordinal))
                {
                    var outcome = await TryPriceSlipAsync(
                        employee, command, overrides, assignmentsByEmployee, componentsById, entry, token);

                    if (outcome.Slip is null)
                    {
                        skipped.Add(new PayrollSkipDto(employee.Id, employee.EmployeeNumber, outcome.SkipReason!));
                        continue;
                    }

                    priced.Add(outcome);
                }

                if (priced.Count == 0)
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.NoEligibleEmployees,
                        $"Payroll run '{entry.PayrollNumber}' priced zero slips for company '{company.Id}' "
                        + $"in [{command.StartDate:yyyy-MM-dd}..{command.EndDate:yyyy-MM-dd}] "
                        + $"({skipped.Count} employees skipped). No voucher is posted.");
                }

                // Replay-safe slip inserts (AS-04/depreciation precedent): an already-present
                // (entry, employee) slip is skipped with its identity, never double-priced.
                // Slip numbers follow employee-number order (the pricing loop order above).
                var created = new List<SalarySlip>(priced.Count);
                foreach (var item in priced)
                {
                    if (await _hr.SlipExistsAsync(entry.Id, item.Slip!.EmployeeId, token))
                    {
                        skipped.Add(new PayrollSkipDto(
                            item.Slip.EmployeeId, item.Employee.EmployeeNumber, "slip_already_exists"));
                        continue;
                    }

                    item.Slip.SlipNumber = $"{entry.PayrollNumber}-{created.Count + 1:D3}";
                    await _hr.AddSlipAsync(item.Slip, token);
                    await _hr.AddSlipLinesAsync(item.Lines!, token);
                    entry.Slips.Add(item.Slip);
                    created.Add(item.Slip);
                }

                if (created.Count == 0)
                {
                    throw new HrValidationException(
                        HrPayrollErrorCodes.NoEligibleEmployees,
                        $"Payroll run '{entry.PayrollNumber}' created zero slips (every priced slip already "
                        + "existed). No voucher is posted.");
                }

                entry.TotalGrossPay = Round4(created.Sum(s => s.GrossPay));
                entry.TotalDeductions = Round4(created.Sum(s => s.TotalDeductions));
                entry.TotalNetPay = Round4(created.Sum(s => s.NetPay));

                var voucherNo = await PostAccrualAsync(company, entry, created, token);
                entry.AccrualVoucherNo = voucherNo;

                entry.Submit();
                await _hr.UpdatePayrollEntryAsync(entry, token);

                return Result<PayrollSubmitResultDto>.Success(new PayrollSubmitResultDto(
                    PayrollEntryDto.Build(entry, created.Count),
                    created.Count,
                    skipped));
            }, cancellationToken);
        }
        catch (HrValidationException ex)
        {
            return Result<PayrollSubmitResultDto>.Failure(ex.Code, ex.Message);
        }
        catch (FiscalPeriodLockedException ex)
        {
            return Result<PayrollSubmitResultDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<PayrollSubmitResultDto>.Failure(ex.Code, ex.Message);
        }
    }

    private static Dictionary<Guid, (int PaymentDays, int AbsentDays)> ValidateOverrides(
        IReadOnlyList<PaymentDayOverride>? source)
    {
        var result = new Dictionary<Guid, (int PaymentDays, int AbsentDays)>();
        if (source is null)
        {
            return result;
        }

        foreach (var entry in source)
        {
            PayrollValidator.EnsureValidPaymentDays(entry.PaymentDays, entry.AbsentDays);
            result[entry.EmployeeId] = (entry.PaymentDays, entry.AbsentDays);
        }

        return result;
    }

    private async Task<PricedSlip> TryPriceSlipAsync(
        Employee employee,
        SubmitPayrollRunCommand command,
        Dictionary<Guid, (int PaymentDays, int AbsentDays)> overrides,
        Dictionary<Guid, IReadOnlyList<SalaryStructureAssignment>> assignmentsByEmployee,
        Dictionary<Guid, SalaryComponent> componentsById,
        PayrollEntry entry,
        CancellationToken token)
    {
        // Spec HR-03: Status == Active, joined on/before period end, not relieved before start.
        if (!employee.IsEligibleForPeriod(command.StartDate, command.EndDate))
        {
            return PricedSlip.Skip(employee, "employee_not_eligible");
        }

        if (!assignmentsByEmployee.TryGetValue(employee.Id, out var employeeAssignments))
        {
            return PricedSlip.Skip(employee, "no_assignment_for_period");
        }

        // Active assignment overlapping the period wins; several overlapping windows would
        // violate the Block A no-overlap rule, so the latest start is the deterministic pick.
        var assignment = employeeAssignments
            .Where(a => a.EffectiveFrom <= command.EndDate
                && (a.EffectiveTo is null || a.EffectiveTo >= command.StartDate))
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefault();

        if (assignment is null)
        {
            return PricedSlip.Skip(employee, "no_assignment_for_period");
        }

        var structure = await _hr.GetStructureByIdAsync(assignment.StructureId, token);
        if (structure is null || structure.CompanyId != command.CompanyId)
        {
            return PricedSlip.Skip(employee, "structure_not_found");
        }

        if (!structure.IsActive)
        {
            return PricedSlip.Skip(employee, "inactive_structure");
        }

        if (structure.Lines.Count == 0)
        {
            return PricedSlip.Skip(employee, "structure_has_no_lines");
        }

        foreach (var line in structure.Lines)
        {
            if (!componentsById.TryGetValue(line.ComponentId, out var component))
            {
                return PricedSlip.Skip(employee, "component_not_found");
            }

            if (!component.IsActive)
            {
                return PricedSlip.Skip(employee, "inactive_component");
            }
        }

        var paymentDays = PayrollValidator.StandardMonthDays;
        var absentDays = 0;
        if (overrides.TryGetValue(employee.Id, out var days))
        {
            paymentDays = days.PaymentDays;
            absentDays = days.AbsentDays;
        }

        // The percentage base is the sum of fixed earning amounts (Block A contract: the caller
        // supplies the base-earnings total; fixed parts cannot depend on themselves).
        var baseEarnings = structure.Lines
            .Where(l => componentsById[l.ComponentId].ComponentType == SalaryComponentType.Earning)
            .Sum(l => l.Amount);

        var factor = (decimal)paymentDays / PayrollValidator.StandardMonthDays;
        var slipLines = new List<SalarySlipLine>(structure.Lines.Count);
        var earnings = 0m;
        var deductions = 0m;

        foreach (var line in structure.Lines.OrderBy(l => l.ComponentId))
        {
            var component = componentsById[line.ComponentId];
            var isEarning = component.ComponentType == SalaryComponentType.Earning;

            // The line math itself runs through the Block A calculator (single-line input) -
            // never reimplemented here. Proration scales the calculator output afterwards.
            var (lineEarnings, lineDeductions) = PayrollCalculator.CalculateStructureTotals(
                new[] { new StructureLineInput(line.Amount, line.PercentageOfBase, isEarning) },
                baseEarnings);

            var value = lineEarnings + lineDeductions;
            if (component.DependsOnPaymentDays)
            {
                value *= factor;
            }

            value = Round4(value);

            slipLines.Add(new SalarySlipLine
            {
                Id = Guid.NewGuid(),
                ComponentId = component.Id,
                ComponentName = component.ComponentName,
                ComponentType = component.ComponentType,
                Amount = value,
            });

            if (isEarning)
            {
                earnings += value;
            }
            else
            {
                deductions += value;
            }
        }

        // HR-01 identity + floor, straight from the Block A calculator (surplus absorbed).
        var (gross, totalDed, net) = PayrollCalculator.CalculateSalarySlip(
            0m, new[] { earnings }, new[] { deductions });

        var slip = new SalarySlip
        {
            Id = Guid.NewGuid(),
            PayrollEntryId = entry.Id,
            EmployeeId = employee.Id,
            PaymentDays = paymentDays,
            AbsentDays = absentDays,
            GrossPay = Round4(gross),
            TotalDeductions = Round4(totalDed),
            NetPay = Round4(net),
            Status = SalarySlipStatus.Draft,
        };

        foreach (var slipLine in slipLines)
        {
            slipLine.SlipId = slip.Id;
        }

        slip.Lines = slipLines;
        slip.Submit();

        return PricedSlip.Priced(employee, slip, slipLines);
    }

    /// <summary>
    /// HR-02 accrual: Dr each distinct earning GL account, Cr each distinct deduction GL
    /// account, Cr PayrollPayable for TotalNetPay. Constitution III.1 balanced BEFORE save.
    /// </summary>
    private async Task<string> PostAccrualAsync(
        Company company,
        PayrollEntry entry,
        IReadOnlyList<SalarySlip> slips,
        CancellationToken token)
    {
        var voucherNo = await _hr.NextVoucherNumberAsync(
            company.Id, VoucherPrefix, entry.PostingDate.Year, token);

        var earningsByAccount = new Dictionary<Guid, decimal>();
        var deductionsByAccount = new Dictionary<Guid, decimal>();
        var accountsById = new Dictionary<Guid, Account>();

        foreach (var slip in slips)
        {
            foreach (var line in slip.Lines)
            {
                var component = await _hr.GetComponentByIdAsync(line.ComponentId, token)
                    ?? throw new HrValidationException(
                        HrPayrollErrorCodes.ComponentNotFound,
                        $"Salary component '{line.ComponentId}' of slip '{slip.SlipNumber}' was not found in this tenant.");

                var account = await HrAccountGuards.RequirePostableAccountAsync(
                    _accounts, component.DefaultGLAccountId, company.Id,
                    $"salary component '{component.ComponentName}'", token);

                accountsById[account.Id] = account;
                var bucket = line.ComponentType == SalaryComponentType.Earning
                    ? earningsByAccount
                    : deductionsByAccount;

                bucket[account.Id] = bucket.TryGetValue(account.Id, out var running)
                    ? running + line.Amount
                    : line.Amount;
            }
        }

        var payableAccount = await HrAccountGuards.RequireAccountByCodeAsync(
            _accounts, company.Id, company.PayrollPayableAccountCode,
            "Company.PayrollPayableAccountCode", token);

        var glLines = new List<GLEntry>(
            earningsByAccount.Count + deductionsByAccount.Count + 1);

        foreach (var (accountId, amount) in earningsByAccount.OrderBy(kv => kv.Key))
        {
            var rounded = Round4(amount);
            if (rounded == 0m)
            {
                continue;
            }

            glLines.Add(NewGlLine(company.Id, entry, accountsById[accountId], voucherNo,
                $"Payroll accrual {entry.PayrollNumber}: earnings", debit: rounded, credit: 0m));
        }

        // HR-01 clamp absorption (DESIGN DECISION, documented): when slips clamp (TotalNetPay
        // below gross minus deductions), crediting the full statutory amounts would overstate
        // liabilities AND unbalance the voucher (Dr gross < Cr deductions + Cr 0). The voucher
        // therefore credits only the EFFECTIVE deductions (gross - net), spread pro-rata across
        // deduction accounts with rounding dust on the largest bucket. The slips keep their full
        // itemized amounts with net zero - the surplus is absorbed, never carried forward.
        var grossTotal = Round4(earningsByAccount.Values.Sum());
        var dedTotal = Round4(deductionsByAccount.Values.Sum());
        var effectiveDed = Round4(grossTotal - entry.TotalNetPay);
        var scale = dedTotal == 0m ? 1m : effectiveDed / dedTotal;

        var orderedDeductions = deductionsByAccount.OrderBy(kv => kv.Key).ToList();
        var scaledAmounts = orderedDeductions.Select(kv => Round4(kv.Value * scale)).ToList();
        var dust = Round4(effectiveDed - scaledAmounts.Sum());
        if (dust != 0m && scaledAmounts.Count > 0)
        {
            var largest = 0;
            for (var i = 1; i < scaledAmounts.Count; i++)
            {
                if (scaledAmounts[i] > scaledAmounts[largest])
                {
                    largest = i;
                }
            }

            scaledAmounts[largest] = Round4(scaledAmounts[largest] + dust);
        }

        for (var i = 0; i < orderedDeductions.Count; i++)
        {
            if (scaledAmounts[i] == 0m)
            {
                continue;
            }

            glLines.Add(NewGlLine(company.Id, entry, accountsById[orderedDeductions[i].Key], voucherNo,
                $"Payroll accrual {entry.PayrollNumber}: deductions", debit: 0m, credit: scaledAmounts[i]));
        }

        glLines.Add(NewGlLine(company.Id, entry, payableAccount, voucherNo,
            $"Payroll accrual {entry.PayrollNumber}: net payable", debit: 0m, credit: Round4(entry.TotalNetPay)));

        // Constitution III.1: the voucher balances BEFORE anything is saved.
        DoubleEntryGuard.EnsureBalanced(glLines);

        await _hr.AddGlEntriesAsync(glLines, token);
        return voucherNo;
    }

    private static GLEntry NewGlLine(
        Guid companyId,
        PayrollEntry entry,
        Account account,
        string voucherNo,
        string remarks,
        decimal debit,
        decimal credit) =>
        new()
        {
            CompanyId = companyId,
            PostingDate = entry.PostingDate,
            AccountId = account.Id,
            Account = account,
            Debit = debit,
            Credit = credit,
            DebitInAccountCurrency = debit,
            CreditInAccountCurrency = credit,
            AccountCurrency = account.Currency?.Code ?? "USD",
            VoucherType = VoucherType,
            VoucherNo = voucherNo,
            VoucherId = entry.Id,
            PartyType = null,
            PartyId = null,
            CostCenterId = null,
            IsCancelled = false,
            Remarks = remarks,
        };

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Drains a paged list read for batch processing (the submit run must see every eligible row,
    /// so a single page is never enough). Pages at <c>MaxPageSize</c> and stops at the first
    /// short page — no unbounded query, no silent 500-row truncation.
    /// </summary>
    private static async Task<List<T>> LoadAllAsync<T>(
        Func<PagedRequest, CancellationToken, Task<PagedResult<T>>> getPage,
        CancellationToken cancellationToken)
    {
        var all = new List<T>();
        var pageNumber = 1;

        while (true)
        {
            var page = await getPage(new PagedRequest(pageNumber, PagedRequest.MaxPageSize), cancellationToken);
            all.AddRange(page.Items);

            if (page.Items.Count < PagedRequest.MaxPageSize)
            {
                break;
            }

            pageNumber++;
        }

        return all;
    }

    /// <summary>One employee pricing outcome: either a priced slip or a skip reason (never both).</summary>
    private sealed record PricedSlip(
        Employee Employee,
        SalarySlip? Slip,
        IReadOnlyList<SalarySlipLine>? Lines,
        string? SkipReason)
    {
        public static PricedSlip Priced(Employee employee, SalarySlip slip, IReadOnlyList<SalarySlipLine> lines) =>
            new(employee, slip, lines, null);

        public static PricedSlip Skip(Employee employee, string reason) =>
            new(employee, null, null, reason);
    }
}
