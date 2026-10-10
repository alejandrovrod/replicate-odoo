namespace AssetHub.Domain.Exceptions;

/// <summary>
/// The supplied asset template schema cannot be compiled.
/// Mapped to HTTP 400 with an RFC 7807 body by ExceptionHandlingMiddleware.
/// </summary>
public class InvalidTemplateSchemaException : DomainException
{
    public InvalidTemplateSchemaException(string detail)
        : base("invalid_template_schema", $"Invalid SchemaJson: {detail}", "Domain.InvalidSchemaJson", detail)
    {
    }
}
