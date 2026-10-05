namespace Erp.Application.Features.Crm.DTOs;

/// <summary>
/// Ingest outcome (Block B, spec CRM-04): the lead plus whether it is a webhook replay.
/// <c>Duplicate</c> is true when the (CompanyId, Source, DeduplicationKey) triple already
/// existed - the existing lead is returned and ZERO rows are written.
/// </summary>
public sealed record IngestLeadResultDto(LeadDto Lead, bool Duplicate);
