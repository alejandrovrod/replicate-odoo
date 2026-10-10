namespace AssetHub.Domain.Exceptions;

/// <summary>
/// The business entity type is referenced by existing templates, so it cannot be deleted.
/// Mapped to HTTP 409 with an RFC 7807 body by ExceptionHandlingMiddleware.
/// </summary>
public class BusinessEntityTypeInUseException : DomainException
{
    public BusinessEntityTypeInUseException(int usages)
        : base(
            "business_entity_type_in_use",
            $"The entity type cannot be deleted. It is being used by {usages} templates.",
            "Domain.EntityTypeInUse",
            usages)
    {
    }
}
