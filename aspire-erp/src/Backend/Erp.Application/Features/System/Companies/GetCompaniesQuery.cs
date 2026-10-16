using System.Collections.Generic;
using Erp.Application.Common;

namespace Erp.Application.Features.SystemBase.Companies;

/// <summary>
/// Loads every company of the current tenant (ordered by name). Backs the company picker that
/// lets a Supplier carry per-company payable/advance accounts (module buying-suppliers, spec
/// scenario SUP-G1: "contabilidad multi-empresa"). Read-only catalog, so no <c>Result</c> wrapper.
/// </summary>
public record GetCompaniesQuery : IQuery<IReadOnlyList<CompanyDto>>;
