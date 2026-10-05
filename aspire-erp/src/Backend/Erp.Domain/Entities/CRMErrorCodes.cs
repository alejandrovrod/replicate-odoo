namespace Erp.Domain.Entities;

/// <summary>
/// Domain error catalog for the CRM module.
/// </summary>
public static class CRMErrorCodes
{
    public const string CompanyRequired = "crm_company_required";
    public const string LeadCodeRequired = "crm_lead_code_required";
    public const string LeadCodeTooLong = "crm_lead_code_too_long";
    public const string LeadNameRequired = "crm_lead_name_required";
    public const string LeadNameTooLong = "crm_lead_name_too_long";
    public const string InvalidEmail = "crm_invalid_email";
    public const string InvalidPhone = "crm_invalid_phone";
    public const string InvalidSource = "crm_invalid_source";
    public const string LossReasonRequired = "crm_loss_reason_required";
    public const string OpportunityAlreadyClosed = "crm_opportunity_already_closed";
    public const string LeadAlreadyConverted = "crm_lead_already_converted";
    public const string InvalidProbabilityRange = "crm_invalid_probability_range";
    public const string InvalidStatusTransition = "invalid_status_transition";
    public const string InvalidOpportunityStage = "crm_invalid_opportunity_stage";
    public const string OpportunityNotWon = "crm_opportunity_not_won";
    public const string OpportunityNoCustomer = "crm_opportunity_no_customer";
}
