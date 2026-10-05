using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Erp.Api.IntegrationTests;

/// <summary>
/// Spec 00-i18n acceptance (tasks B-10..B-12): the RFC 7807 <c>title</c>/<c>detail</c> of a
/// rejection localize per <c>Accept-Language</c> while the <c>code</c> extension and the status
/// line stay the invariant English contract (principle: domain data is never translated).
/// </summary>
/// <remarks>
/// Every scenario uses the request-validation or not-found paths only - zero rows are created,
/// so this class is safe outside <see cref="LedgerMutatingCollection"/> (the AccountsApiTests
/// precedent). English values come from the culture-less (neutral) resx: .NET's fallback chain
/// for "es" is es -&gt; neutral, which is exactly why English lives in ErrorMessages.resx and
/// not ErrorMessages.en.resx.
/// </remarks>
public class ErrorLocalizationApiTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public ErrorLocalizationApiTests(ErpApiFactory factory) => _factory = factory;

    /// <summary>
    /// B-10: <c>Accept-Language: es</c> localizes both ProblemDetails fields to Spanish for a
    /// controller-level validation, with <c>crm_company_required</c> unchanged.
    /// </summary>
    [Fact]
    public async Task OpportunitiesList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/opportunities?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "crm_company_required");
        Assert.Equal("Se requiere una empresa.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// B-10: the same request with <c>Accept-Language: en</c> answers the neutral-resource
    /// English text (the default culture resolves against the culture-less resx).
    /// </summary>
    [Fact]
    public async Task OpportunitiesList_EmptyCompanyId_WithEnHeader_ReturnsEnglishProblemDetails()
    {
        using var client = CreateClient("en");
        using var response = await client.GetAsync(
            $"/api/v1/opportunities?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Invalid Company", "crm_company_required");
        Assert.Equal("A company is required.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// B-12: an unsupported language never breaks the response - the middleware falls back to
    /// the default culture and the caller still gets complete English ProblemDetails.
    /// </summary>
    [Fact]
    public async Task OpportunitiesList_EmptyCompanyId_WithUnsupportedHeader_FallsBackToEnglish()
    {
        using var client = CreateClient("fr-FR");
        using var response = await client.GetAsync(
            $"/api/v1/opportunities?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Invalid Company", "crm_company_required");
        Assert.Equal("A company is required.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// B-11: the Result&lt;T&gt; -&gt; ProblemDetails path localizes too - a domain not-found
    /// (404 crm_opportunity_not_found raised by the advance handler) carries Spanish
    /// title/detail under <c>Accept-Language: es</c> while the machine code stays invariant.
    /// </summary>
    [Fact]
    public async Task Advance_MissingOpportunity_WithEsHeader_Returns404SpanishFromDomainError()
    {
        using var client = CreateClient("es");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/opportunities/{Guid.NewGuid()}/advance?companyId={ErpApiFactory.DevCompanyId}")
        {
            Content = JsonContent.Create(new { toStage = "Proposal" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "Oportunidad no encontrada", "crm_opportunity_not_found");
        Assert.Equal("Oportunidad no encontrada.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// B-16: the domain-validation path localizes too. There is no FluentValidation in this
    /// solution - <c>LeadValidator</c> throws <c>CRMValidationException(code, english)</c>, the
    /// handler maps it to <c>Result.Fail</c> and the controller resolves <c>code</c> against
    /// <c>ErrorMessages</c>. The Spanish detail must come from the satellite resx, never from
    /// the exception's English message. The rule fails before any insert, so zero rows are
    /// written (safe outside <see cref="LedgerMutatingCollection"/>).
    /// </summary>
    [Fact]
    public async Task Ingest_LeadCodeTooLong_WithEsHeader_ReturnsSpanishDomainValidationError()
    {
        using var client = CreateClient("es");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/leads/ingest")
        {
            Content = JsonContent.Create(new
            {
                companyId = ErpApiFactory.DevCompanyId,
                leadCode = new string('X', 51),
                leadName = "Validation Vera",
                organizationName = "TechCorp",
                email = "vera@techcorp.example",
                phone = "+1-555-0101",
                source = "Website",
                deduplicationKey = $"i18n-dom-{Guid.NewGuid():N}",
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Lead rechazado", "crm_lead_code_too_long");
        Assert.Equal(
            "El código del lead supera la longitud máxima.",
            problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (Stock): the same controller-guard pattern in a refactored module — an empty
    /// <c>companyId</c> on the stock entries list resolves title and detail from the catalog
    /// under <c>Accept-Language: es</c>. Zero writes, safe outside
    /// <see cref="LedgerMutatingCollection"/>.
    /// </summary>
    [Fact]
    public async Task StockEntriesList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/stockentries?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "company_not_found");
        Assert.Equal("Empresa no encontrada.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (Selling): the same zero-write guard in another refactored module — the customers
    /// list with an empty <c>companyId</c> resolves title and detail from the catalog under
    /// <c>Accept-Language: es</c>.
    /// </summary>
    [Fact]
    public async Task CustomersList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/customers?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "company_required");
        Assert.Equal("Se requiere una empresa.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (Buying): the same zero-write guard in another refactored module — the purchase
    /// orders list with an empty <c>companyId</c> resolves title and detail from the catalog
    /// under <c>Accept-Language: es</c>.
    /// </summary>
    [Fact]
    public async Task PurchaseOrdersList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/purchaseorders?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "company_not_found");
        Assert.Equal("Empresa no encontrada.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (Manufacturing): the same zero-write guard in another refactored module — the BOM
    /// list with an empty <c>companyId</c> resolves title and detail from the catalog under
    /// <c>Accept-Language: es</c>.
    /// </summary>
    [Fact]
    public async Task BomsList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/boms?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "bom_not_found");
        Assert.Equal("Lista de materiales no encontrada.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (Banking): the same zero-write guard in another refactored module — the rules list
    /// with an empty <c>companyId</c> resolves title and detail from the catalog under
    /// <c>Accept-Language: es</c>.
    /// </summary>
    [Fact]
    public async Task BankRulesList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/bank-transaction-rules?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "bank_transaction_rule_not_found");
        Assert.Equal("Regla de transacción bancaria no encontrada.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (Accounting): the same zero-write guard in another refactored module — the general
    /// ledger report with an empty <c>companyId</c> resolves title and detail from the catalog
    /// under <c>Accept-Language: es</c>.
    /// </summary>
    [Fact]
    public async Task GeneralLedgerReport_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/financialreports/general-ledger?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "company_required");
        Assert.Equal("Se requiere una empresa.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (HR/Payroll): the same zero-write guard in another refactored module — the
    /// employees list with an empty <c>companyId</c> resolves title and detail from the catalog
    /// under <c>Accept-Language: es</c>.
    /// </summary>
    [Fact]
    public async Task EmployeesList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/hr/employees?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "company_not_found");
        Assert.Equal("Empresa no encontrada.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// Phase 2 (Assets): the same zero-write guard in the last refactored module — the assets
    /// list with an empty <c>companyId</c> resolves title and detail from the catalog under
    /// <c>Accept-Language: es</c>.
    /// </summary>
    [Fact]
    public async Task AssetsList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails()
    {
        using var client = CreateClient("es");
        using var response = await client.GetAsync(
            $"/api/v1/assets?companyId={Guid.Empty}");

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Empresa inválida", "company_not_found");
        Assert.Equal("Empresa no encontrada.", problem["detail"]!.GetValue<string>());
    }

    /// <summary>Tenant-scoped client with an explicit Accept-Language (ErpApiFactory contract).</summary>
    private HttpClient CreateClient(string acceptLanguage)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", ErpApiFactory.DevTenantId.ToString());
        client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        return client;
    }

    /// <summary>Asserts the RFC 7807 shape: status, extension, localized title, invariant code.</summary>
    private static async Task<JsonNode> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal((int)expectedStatus, problem["status"]!.GetValue<int>());
        Assert.Equal(expectedTitle, problem["title"]!.GetValue<string>());
        Assert.Equal(expectedCode, problem["code"]!.GetValue<string>());
        return problem;
    }
}
