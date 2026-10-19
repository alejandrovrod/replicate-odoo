using Erp.Application.Common;
using Erp.Application.Features.Crm.DTOs;
using System;
using System.Collections.Generic;

namespace Erp.Application.Features.Crm.Commands;

/// <summary>
/// One priced-line input of the commercial dossier (full-replace semantics: the list
/// travels complete on every edit, exactly like the invoice-lines precedent).
/// </summary>
public sealed record OpportunityItemInput(
    Guid ItemId,
    decimal Quantity,
    decimal Rate,
    decimal DiscountPercentage = 0m);

/// <summary>
/// Updates the commercial dossier of an Open deal (CRM/ERPNext parity): priced lines
/// (full replace), segmentation, competitors, currency/rate, expected date, probability
/// and assignment. Patch semantics: null leaves a field unchanged, <see cref="Guid.Empty"/>
/// (or empty text) clears it; <c>Items</c> null leaves the lines alone while an empty list
/// clears them. Closed deals reject with <c>crm_opportunity_not_open</c> - advance/reopen
/// own the terminal transitions. <c>RowVersion</c> is mandatory: the edit form always
/// carries the token it read.
/// </summary>
public sealed record UpdateOpportunityCommand(
    Guid OpportunityId,
    Guid CompanyId,
    byte[] RowVersion,
    string? PartyName = null,
    decimal? Probability = null,
    DateOnly? ExpectedClosingDate = null,
    Guid? AssignedSalespersonId = null,
    Guid? CurrencyId = null,
    bool ClearCurrency = false,
    decimal? ExchangeRate = null,
    Guid? TerritoryId = null,
    Guid? IndustryId = null,
    string? MarketSegment = null,
    IReadOnlyList<string>? Competitors = null,
    IReadOnlyList<OpportunityItemInput>? Items = null
) : ICommand<Result<OpportunityDetailDto>>;
