namespace Erp.Api.Common;

/// <summary>
/// Marker class for <c>IStringLocalizer&lt;CommonMessages&gt;</c>: localized RFC 7807 titles
/// (Opportunity Rejected, Sales Order Conflict, ...) keyed by PascalCase title keys, while
/// <see cref="Shared.ErrorMessages"/> keys the human-readable details by error code.
/// </summary>
/// <remarks>
/// Namespace must stay <c>Erp.Api.Common</c> so the resource prefix resolves to
/// <c>Erp.Api.Resources.Common.CommonMessages</c>, matching Resources/Common/CommonMessages.resx.
/// </remarks>
public sealed class CommonMessages { }
