namespace Erp.Domain.Exceptions;

/// <summary>
/// Webhook replay race (spec CRM-04, fix-pass W1): two concurrent ingests carrying the same
/// <c>(CompanyId, Source, ExternalReference)</c> triple both passed the handler's
/// <c>GetLeadByDedupKeyAsync</c> pre-check, and the filtered unique index
/// <c>UQ_Lead_Company_Source_ExternalRef</c> rejected the loser's insert (SQL 2601/2627).
/// <c>CrmRepository.AddLeadAsync</c> translates that race into this typed failure - the
/// CustomerRepository duplicate-code precedent - so <c>IngestLeadCommandHandler</c> can
/// re-read the winner and answer <c>Duplicate=true</c> instead of leaking an EF/SQL
/// exception to the API layer.
/// </summary>
public sealed class DuplicateLeadException : Exception
{
    public Guid CompanyId { get; }

    public string LeadSource { get; }

    public string ExternalReference { get; }

    public DuplicateLeadException(
        Guid companyId,
        string leadSource,
        string externalReference,
        Exception? innerException = null)
        : base(
            $"A lead for company '{companyId}' from source '{leadSource}' with external "
            + $"reference '{externalReference}' was created concurrently (webhook replay race).",
            innerException)
    {
        CompanyId = companyId;
        LeadSource = leadSource;
        ExternalReference = externalReference;
    }
}
