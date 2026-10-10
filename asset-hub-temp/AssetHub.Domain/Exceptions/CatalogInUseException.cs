namespace AssetHub.Domain.Exceptions;

public class CatalogInUseException : DomainException
{
    public CatalogInUseException(string detail)
        : base(
            "catalog_in_use",
            $"The catalog item cannot be deleted because it is in use. {detail}",
            "Domain.CatalogInUse",
            detail)
    {
    }
}
