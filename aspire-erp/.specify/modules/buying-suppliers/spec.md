# Spec — Supplier Master Data & Governance (ERPNext Parity)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, ERPNext Parity, Strict Multi-Company Accounting, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

En ERPNext y sistemas ERP empresariales, el maestro de proveedores (**Supplier**) no es una libreta de contactos pasiva, sino el núcleo de la cadena de suministro y cuentas por pagar (**Accounts Payable**). 

Actualmente, el sistema posee un CRUD mínimo (`code`, `name`, `taxId`, `defaultPayableAccountId`, `currencyId`, `isActive`). Para alcanzar paridad funcional con ERPNext (`buying/doctype/supplier/supplier.json`), el proveedor debe gobernar:
1. **Clasificación jerárquica** (`SupplierGroup`) para parametrización contable y de compras.
2. **Cuentas contables por empresa** (`SupplierAccount` / Multi-Company Party Account): en entornos multi-compañía, un proveedor tiene diferentes cuentas por pagar y anticipos según la sociedad jurídica que le compra.
3. **Condiciones comerciales y de crédito**: términos de pago (`PaymentTermsTemplate`), listas de precios de compra por defecto (`PriceList`) y monedas operativas.
4. **Gobierno de transacciones y estados de retención (`Hold Status`)**: capacidad de congelar/suspender órdenes de compra, facturas de compra o pagos con fecha de vencimiento (`Release Date`).
5. **Categorización tributaria y retenciones fiscales**: categoría fiscal (`TaxCategory`) y configuración de retención impositiva en la fuente (`TaxWithholdingCategory`).

---

## 1. Gap Matrix (Actual vs ERPNext)

| # | Dimensión | Estado Actual | Paridad ERPNext a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------------------|--------------------------|
| D1 | Clasificación | Inexistente (texto plano o nulo) | `SupplierGroup` (ej: Local, Exterior, Servicios, Materias Primas) | Entidad `SupplierGroup` con soporte jerárquico o catálogo y relación 1:N obligatoria con `Supplier`. |
| D2 | Multi-Company Accounting | Un solo campo `DefaultPayableAccountId` plano en `Supplier` | Tabla hija `SupplierAccount` (`CompanyId`, `PayableAccountId`, `AdvanceAccountId`) | Entidad hija `SupplierAccount` con índice único compuesto (`SupplierId`, `CompanyId`). |
| D3 | Términos Comerciales | `paymentTermsDays` (entero simple) | `PaymentTermsTemplateId` y `DefaultPriceListId` | Soporte para plantillas de pago escalonadas y lista de precios de compra asociada. |
| D4 | Bloqueo y Gobierno | `isActive` (booleano binario) | `IsFrozen`, `OnHold`, `HoldType` (`Invoices`, `Payments`, `All`), `ReleaseDate` | Value Object / Propiedades de gobierno en el Aggregate Root con validaciones de guard en Compras. |
| D5 | Fiscalidad y Retenciones | `taxId` (texto genérico) | `TaxId`, `TaxCategory`, `TaxWithholdingCategory` | Campos estructurados para resolver regímenes fiscales en facturación. |
| D6 | UI Maestro y Formulario | Modal plano simple de 6 campos | Formulario estructurado con pestañas (`Detalles`, `Contabilidad Multi-empresa`, `Gobierno y Bloqueo`, `Fiscal`) | Tabs en `SupplierFormModal` con sub-tablas para cuentas por empresa. |

---

## 2. Escenarios Gherkin

```gherkin
Feature: Supplier Master Data & Governance
  Background:
    Given a Tenant with active Company "COMP-001" and Company "COMP-002"
    And a Supplier Group "Raw Materials"
    And Payable Account "2101-Local" for "COMP-001"
    And Payable Account "2102-Foreign" for "COMP-002"

  Scenario: SUP-G1 Creación de proveedor con grupo y cuentas multi-empresa
    When creating a Supplier with:
      | Code | Name | Group | Tax ID |
      | SUP-2026-001 | Tech Supplies SA | Raw Materials | 30-71234567-9 |
    And assigning company accounts:
      | Company | Account |
      | COMP-001 | 2101-Local |
      | COMP-002 | 2102-Foreign |
    Then the Supplier is persisted with active status
    And queries for COMP-001 resolve payable account to "2101-Local"
    And queries for COMP-002 resolve payable account to "2102-Foreign"

  Scenario: SUP-G2 Bloqueo de facturación a proveedor en retención (On Hold)
    Given a Supplier "SUP-2026-001" with:
      | OnHold | HoldType | ReleaseDate |
      | true | Invoices | 2026-12-31 |
    When attempting to submit a Purchase Invoice for "SUP-2026-001" on date "2026-10-15"
    Then the operation is rejected with 422 ProblemDetails
    And the error code is "SUPPLIER_ON_HOLD_INVOICES"

  Scenario: SUP-G3 Liberación automática de retención pasada la fecha de vencimiento
    Given a Supplier "SUP-2026-001" with:
      | OnHold | HoldType | ReleaseDate |
      | true | Invoices | 2026-10-01 |
    When creating a Purchase Invoice for "SUP-2026-001" on date "2026-10-15"
    Then the operation succeeds because the release date has elapsed

  Scenario: SUP-G4 Integridad de concurrencia optimista
    Given an existing Supplier with RowVersion "AAAAAAA="
    When User A submits an update with RowVersion "AAAAAAA="
    And User B submits a concurrent update with RowVersion "AAAAAAA="
    Then User A update succeeds and increments RowVersion
    And User B update is rejected with 409 Conflict ProblemDetails
```

---

## 3. Modelo de Dominio (.NET 10 / C#)

```csharp
public enum SupplierHoldType
{
    None = 0,
    Invoices = 1,
    Payments = 2,
    All = 3
}

public class Supplier : AggregateRoot<Guid>, ITenantEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Guid SupplierGroupId { get; private set; }
    public SupplierGroup SupplierGroup { get; private set; } = null!;
    public string? TaxId { get; private set; }
    public string? CurrencyId { get; private set; }
    public Guid? PaymentTermsTemplateId { get; private set; }
    public Guid? DefaultPriceListId { get; private set; }
    
    // Status & Governance
    public bool IsActive { get; private set; } = true;
    public bool IsFrozen { get; private set; }
    public bool OnHold { get; private set; }
    public SupplierHoldType HoldType { get; private set; }
    public DateOnly? ReleaseDate { get; private set; }

    // Multi-Company Accounts
    private readonly List<SupplierCompanyAccount> _accounts = new();
    public IReadOnlyCollection<SupplierCompanyAccount> Accounts => _accounts.AsReadOnly();

    public byte[] RowVersion { get; private set; } = null!;

    public bool IsBlockedFor(SupplierHoldType requestedAction, DateOnly currentDate)
    {
        if (!IsActive) return true;
        if (!OnHold) return false;
        if (ReleaseDate.HasValue && currentDate > ReleaseDate.Value) return false;
        return HoldType == SupplierHoldType.All || HoldType == requestedAction;
    }

    public void SetCompanyAccount(Guid companyId, Guid payableAccountId, Guid? advanceAccountId = null)
    {
        var existing = _accounts.FirstOrDefault(a => a.CompanyId == companyId);
        if (existing != null)
        {
            existing.Update(payableAccountId, advanceAccountId);
        }
        else
        {
            _accounts.Add(new SupplierCompanyAccount(Id, companyId, payableAccountId, advanceAccountId));
        }
    }
}

public class SupplierCompanyAccount : Entity<Guid>
{
    public Guid SupplierId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid PayableAccountId { get; private set; }
    public Guid? AdvanceAccountId { get; private set; }

    public SupplierCompanyAccount(Guid supplierId, Guid companyId, Guid payableAccountId, Guid? advanceAccountId)
    {
        SupplierId = supplierId;
        CompanyId = companyId;
        PayableAccountId = payableAccountId;
        AdvanceAccountId = advanceAccountId;
    }

    public void Update(Guid payableAccountId, Guid? advanceAccountId)
    {
        PayableAccountId = payableAccountId;
        AdvanceAccountId = advanceAccountId;
    }
}
```

---

## 4. DDL & Migración SQL Server 2025

```sql
CREATE TABLE [buying].[SupplierGroups] (
    [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [Name] NVARCHAR(150) NOT NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_SupplierGroups] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_SupplierGroups_Tenant_Code] UNIQUE ([TenantId], [Code])
);

ALTER TABLE [buying].[Suppliers] ADD
    [SupplierGroupId] UNIQUEIDENTIFIER NULL,
    [PaymentTermsTemplateId] UNIQUEIDENTIFIER NULL,
    [DefaultPriceListId] UNIQUEIDENTIFIER NULL,
    [IsFrozen] BIT NOT NULL DEFAULT 0,
    [OnHold] BIT NOT NULL DEFAULT 0,
    [HoldType] INT NOT NULL DEFAULT 0,
    [ReleaseDate] DATE NULL;

CREATE TABLE [buying].[SupplierCompanyAccounts] (
    [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    [SupplierId] UNIQUEIDENTIFIER NOT NULL,
    [CompanyId] UNIQUEIDENTIFIER NOT NULL,
    [PayableAccountId] UNIQUEIDENTIFIER NOT NULL,
    [AdvanceAccountId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_SupplierCompanyAccounts] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_SupplierCompanyAccounts_Supplier] FOREIGN KEY ([SupplierId]) 
        REFERENCES [buying].[Suppliers]([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_SupplierCompanyAccounts_Supplier_Company] UNIQUE ([SupplierId], [CompanyId])
);

CREATE NONCLUSTERED INDEX [IX_Suppliers_Tenant_Group] ON [buying].[Suppliers] ([TenantId], [SupplierGroupId]);
```

---

## 5. API Boundaries & RFC 7807 Errors

### Endpoints
- `GET /api/v1/suppliers` (soporta filtros `groupId`, `onHold`, `companyId`)
- `GET /api/v1/suppliers/{id}` (retorna detalle con arreglo de cuentas por compañía)
- `POST /api/v1/suppliers` (crea maestro y cuentas multi-empresa asociadas)
- `PUT /api/v1/suppliers/{id}` (actualización con control de concurrencia `RowVersion`)
- `POST /api/v1/suppliers/{id}/hold` (suspensión / liberación con motivo y fecha)
- `GET /api/v1/supplier-groups` (catálogo de grupos)

### Problem Details Error Codes
- `SUPPLIER_CODE_ALREADY_EXISTS` (409 Conflict)
- `SUPPLIER_CONCURRENCY_CONFLICT` (409 Conflict - RowVersion mismatch)
- `SUPPLIER_ON_HOLD_INVOICES` (422 Unprocessable Entity - proveedor bloqueado para compras)
- `SUPPLIER_ON_HOLD_PAYMENTS` (422 Unprocessable Entity - proveedor bloqueado para pagos)
- `SUPPLIER_GROUP_NOT_FOUND` (404 Not Found)
