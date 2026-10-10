namespace AssetHub.Domain.Exceptions;

public class ModuleNotEnabledException : DomainException
{
    public ModuleNotEnabledException(string moduleName)
        : base(
            "module_not_enabled",
            $"The '{moduleName}' module is not enabled in your current plan.",
            "Domain.ModuleNotEnabled",
            moduleName)
    {
    }
}
