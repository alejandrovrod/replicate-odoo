# Spec — Asset Capitalization & Acquisition Flow (Capitalización Automática de Activos)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean/Hexagonal Architecture, DDD, Strict Accounting Ledger Invariants, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

Actualmente el módulo de activos fijos (`AssetsController`) no permite la creación aislada o "en frío" de activos fijos para salvaguardar la integridad de la partida doble y el libro mayor (General Ledger).

Este módulo implementa el flujo contable y operativo para:
1. **Capitalización vía Factura de Compra (`PurchaseInvoice`)**: Al adquirir un ítem marcado como activo (`IsFixedAsset = true`), la contabilización de la factura genera la provisión/gasto y crea automáticamente la ficha del activo fijo en estado `Capitalized` o `Draft` con su cuenta de capitalización (`CWIP` o `Asset Cost Account`).
2. **Capitalización vía Asiento de Apertura (`Opening Journal Entry`)**: Migración de activos existentes con depreciación acumulada histórica sin alterar resultados del ejercicio.
3. **Calendario y Registro de Depreciaciones**: Cálculo lineal mensual automático y generación de pólizas contables automáticas debitando la cuenta de gasto de depreciación y acreditando depreciación acumulada.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Origen del Activo | Solo CRUD desconectado de contabilidad | No hay trigger en `SubmitPurchaseInvoiceCommandHandler` para emitir activos | Event-driven / Pipeline handler: al someter `PurchaseInvoice` con ítems de activo, generar registro en `Asset` con referencia a `PurchaseInvoiceId` |
| D2 | Categoría de Activo | `AssetCategory` existe con cuentas GL | Falta validar que `AssetCategory` defina `AssetCostAccount`, `DepreciationAccount`, `AccumulatedDepreciationAccount` y `CwipAccount` | Obligatorio tener cuentas configuradas antes de permitir someter facturas con activos fijos |
| D3 | Invariantes Contables | Asientos manuales pueden descuadrar | Asiento de adquisición automático: Dr Cuenta Activo Fijo (o CWIP) / Cr Cuentas por Pagar (Supplier) | El `Asset` se crea con `GrossPurchaseAmount = LineTotal`, `DepreciationStartDate`, `Status = Capitalized` |
| D4 | Tabla y DDL | `Asset` tiene campos básicos | Falta `PurchaseInvoiceId`, `PurchaseInvoiceItemId`, `JournalEntryId`, `DepreciationSchedule` | Relación FK con `PurchaseInvoice` opcional (nullable para soportar apertura) |
| D5 | Errores / RFC 7807 | Excepciones genéricas | Faltan códigos de error: `asset_category_missing_accounts`, `cannot_capitalize_unsubmitted_invoice` | Centralizados en `AssetErrorCodes` y mapeados a RFC 7807 ProblemDetails |

---

## 2. Escenarios Gherkin (AST-CAP-G1..G4)

```gherkin
Feature: Asset Capitalization Flow
  Background:
    Given a configured Asset Category "Vehículos" with Cost Account "1501-01" and Accumulated Depr "1502-01"
    And a verified Supplier "Auto Distribuidora SA"

  Scenario: AST-CAP-G1 Capitalización automática desde Factura de Compra
    Given a Purchase Invoice with 1 item "Camión Reparto" of category "Vehículos" for amount 500000
    When the Purchase Invoice is submitted
    Then the General Ledger records Dr 1501-01 for 500000 and Cr Accounts Payable for 500000
    And a new Asset record is created with code starting with "AST-", status "Capitalized" and gross purchase amount 500000
    And the Asset links back to the PurchaseInvoiceId

  Scenario: AST-CAP-G2 Capitalización por Asiento de Apertura (Migración)
    When a Journal Entry of type "Opening Entry" contains Dr Asset Cost 200000 and Cr Accumulated Depreciation 50000
    And references an Asset Draft
    Then the Asset status changes to "Capitalized" with accumulated depreciation initialized to 50000

  Scenario: AST-CAP-G3 Rechazo de factura con categoría de activo sin cuentas configuradas
    Given an Asset Category with missing AssetCostAccountId
    When a Purchase Invoice tries to submit with this category
    Then the system rejects submission with 400 ProblemDetails and code "asset_category_missing_accounts"

  Scenario: AST-CAP-G4 Cancelación de Factura reversa el Activo
    Given a Capitalized Asset originating from a Purchase Invoice
    When the Purchase Invoice is cancelled
    Then the Asset status becomes "Cancelled"
    And if any depreciation was posted, cancellation is blocked with "asset_has_depreciation_entries"
```

---

## 3. Modelo de Dominio y Contratos

### Entidades y Modificaciones
```csharp
// Entidad Asset.cs
public class Asset : AggregateRoot<Guid>, ITenantEntity
{
    public string AssetCode { get; private set; }
    public string Name { get; private set; }
    public Guid AssetCategoryId { get; private set; }
    public AssetCategory Category { get; private set; }
    public decimal GrossPurchaseAmount { get; private set; }
    public decimal SalvageValue { get; private set; }
    public DateTime AcquisitionDate { get; private set; }
    public DateTime DepreciationStartDate { get; private set; }
    public int UsefulLifeMonths { get; private set; }
    public AssetStatus Status { get; private set; } // Draft, Capitalized, Disposed, Cancelled
    public Guid? PurchaseInvoiceId { get; private set; }
    public Guid? PurchaseInvoiceItemId { get; private set; }
    public Guid? OpeningJournalEntryId { get; private set; }
    public ICollection<AssetDepreciationSchedule> DepreciationSchedules { get; private set; }
}
```

---

## 4. Plan de Tareas Técnicas (Backend & Frontend)

- **T1 (Dominio & DB)**: Migration EF Core: FKs `PurchaseInvoiceId`, `PurchaseInvoiceItemId` en tabla `Assets`. Guardias de dominio en `Asset.CapitalizeFromPurchase(...)`.
- **T2 (Contabilidad GL)**: Modificar `PurchaseInvoicePosting` para desviar las líneas de artículos activos a la cuenta de costo del activo en lugar de cuentas genéricas de gasto.
- **T3 (Handler & CQRS)**: Integrar creación automática de `Asset` en `SubmitPurchaseInvoiceCommandHandler` (transacción única bajo `IDbContextTransaction`).
- **T4 (Depreciaciones)**: Endpoint `POST /api/v1/assets/{id}/generate-schedule` y `POST /api/v1/assets/depreciate-period`.
- **T5 (Frontend UI)**: Tab de "Activos Fijos Generados" en vista de detalle de Factura de Compra + Botón de trazabilidad "Ver Activo".
