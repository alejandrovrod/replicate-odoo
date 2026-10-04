using Erp.Domain.Common;

namespace Erp.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to bill a quantity that exceeds the remaining billable quantity on a purchase receipt line.
/// </summary>
public class OverbillingNotAllowedException : Exception
{
    public OverbillingNotAllowedException(string message) 
        : base(message)
    {
    }
}
