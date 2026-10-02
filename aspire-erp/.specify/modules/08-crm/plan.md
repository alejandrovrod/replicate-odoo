# Technical Plan: CRM & Sales Pipeline (ERPNext Parity)

**Module:** `08-crm`  
**Status:** APPROVED  
**Version:** 1.0.0  
**Stack:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, Entity Framework Core 9, React 19 + TypeScript  
**Architectural Standard:** Clean Architecture, Weighted Forecasting, Lead Lifecycle  

---

## 1. Microsoft SQL Server 2025 Physical Schema (DDL)

```sql
-- 1. Inbound Lead
CREATE TABLE Lead (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    LeadCode NVARCHAR(50) NOT NULL,
    LeadName NVARCHAR(150) NOT NULL,
    OrganizationName NVARCHAR(150) NULL,
    Email NVARCHAR(150) NULL,
    Phone NVARCHAR(50) NULL,
    Source NVARCHAR(50) NOT NULL DEFAULT 'Website',
    Status NVARCHAR(30) NOT NULL DEFAULT 'Open', -- Open, Contacted, Qualified, Converted, Lost
    AssignedToUserId UNIQUEIDENTIFIER NULL,
    ConvertedOpportunityId UNIQUEIDENTIFIER NULL,
    ConvertedCustomerId UNIQUEIDENTIFIER NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.LeadHistory));

CREATE NONCLUSTERED INDEX IX_Lead_Tenant_Status ON Lead (TenantId, CompanyId, Status);

-- 2. Commercial Opportunity
CREATE TABLE Opportunity (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    OpportunityNumber NVARCHAR(50) NOT NULL,
    OpportunityFrom NVARCHAR(20) NOT NULL DEFAULT 'Lead', -- Lead, Customer
    PartyId UNIQUEIDENTIFIER NOT NULL,
    PartyName NVARCHAR(150) NOT NULL,
    Stage NVARCHAR(30) NOT NULL DEFAULT 'Prospecting', -- Prospecting, Qualification, Proposal, Negotiation, ClosedWon, ClosedLost
    OpportunityAmount DECIMAL(18,4) NOT NULL DEFAULT 0.0000,
    Probability DECIMAL(5,2) NOT NULL DEFAULT 10.00,
    WeightedAmount AS (OpportunityAmount * (Probability / 100.0)),
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    ExpectedClosingDate DATE NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Open', -- Open, Won, Lost, Expired
    LossReason NVARCHAR(MAX) NULL,
    AssignedSalespersonId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_Opportunity_Amount CHECK (OpportunityAmount >= 0.0000),
    CONSTRAINT CK_Opportunity_Probability CHECK (Probability >= 0.00 AND Probability <= 100.00)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.OpportunityHistory));

CREATE NONCLUSTERED INDEX IX_Opportunity_Tenant_Stage ON Opportunity (TenantId, CompanyId, Stage, Status);

-- 3. CRM Activity & Follow-Up Log
CREATE TABLE CRMActivity (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    OpportunityId UNIQUEIDENTIFIER NOT NULL,
    Type NVARCHAR(30) NOT NULL, -- Call, Email, Meeting, Task, Note
    Subject NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX) NULL,
    ActivityDate DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    NextFollowUpDate DATE NULL,
    CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT FK_CRMActivity_Opportunity FOREIGN KEY (OpportunityId) REFERENCES Opportunity(Id) ON DELETE CASCADE
);
```

---

## 2. Domain Error Catalog & Exception Contracts

```csharp
namespace Erp.Domain.Crm.Errors;

public static class CRMErrorCodes
{
    public const string LossReasonRequired = "CRM_LOSS_REASON_REQUIRED";
    public const string OpportunityAlreadyClosed = "CRM_OPPORTUNITY_ALREADY_CLOSED";
    public const string LeadAlreadyConverted = "CRM_LEAD_ALREADY_CONVERTED";
    public const string InvalidProbabilityRange = "CRM_INVALID_PROBABILITY_RANGE";
}
```

---

## 3. Weighted Pipeline Calculation

```csharp
namespace Erp.Domain.Services;

public sealed class PipelineForecaster
{
    public static decimal CalculateWeightedPipeline(IEnumerable<Opportunity> opportunities)
    {
        return opportunities
            .Where(o => o.Status == OpportunityStatus.Open)
            .Sum(o => o.OpportunityAmount * (o.Probability / 100.0m));
    }
}
```
