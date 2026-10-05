using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

public static class OpportunityValidator
{
    public const int MaxNumberLength = 50;
    public const int MaxFromLength = 20;
    public const int MaxPartyNameLength = 150;
    public const int MaxStageLength = 30;
    public const int MaxCurrencyLength = 3;
    public const int MaxStatusLength = 30;

    public static void EnsureValidOpportunityFields(
        Guid companyId,
        string? opportunityNumber,
        string? opportunityFrom,
        Guid partyId,
        string? partyName,
        decimal amount,
        decimal probability,
        string? currency)
    {
        if (companyId == Guid.Empty)
        {
            throw new CRMValidationException(
                CRMErrorCodes.CompanyRequired,
                "An opportunity must belong to a company.");
        }

        if (string.IsNullOrWhiteSpace(opportunityNumber) || opportunityNumber.Length > MaxNumberLength)
        {
            throw new CRMValidationException(
                "crm_opportunity_number_invalid",
                $"Opportunity Number is required and cannot exceed {MaxNumberLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(opportunityFrom) || opportunityFrom.Length > MaxFromLength)
        {
            throw new CRMValidationException(
                "crm_opportunity_from_invalid",
                $"Opportunity From must be 'Lead' or 'Customer' and cannot exceed {MaxFromLength} characters.");
        }

        if (partyId == Guid.Empty)
        {
            throw new CRMValidationException(
                "crm_party_id_required",
                "A PartyId (LeadId or CustomerId) is required.");
        }

        if (string.IsNullOrWhiteSpace(partyName) || partyName.Length > MaxPartyNameLength)
        {
            throw new CRMValidationException(
                "crm_party_name_invalid",
                $"Party Name is required and cannot exceed {MaxPartyNameLength} characters.");
        }

        if (amount < 0)
        {
            throw new CRMValidationException(
                "crm_invalid_opportunity_amount",
                "Opportunity amount cannot be negative.");
        }

        if (probability < 0 || probability > 100)
        {
            throw new CRMValidationException(
                CRMErrorCodes.InvalidProbabilityRange,
                $"Probability must be between 0 and 100. Received {probability}.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length > MaxCurrencyLength)
        {
            throw new CRMValidationException(
                "crm_invalid_currency",
                $"Currency must be a non-empty ISO 4217 code of at most {MaxCurrencyLength} characters.");
        }
    }
}
