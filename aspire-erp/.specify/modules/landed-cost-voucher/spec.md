# Spec — Landed Cost Voucher: Acquisition Cost Apportionment (Fase 4)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, Strict Inventory Valuation, DoubleEntryGuard, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

En empresas importadoras, distribuidoras y fabricantes, el costo final de un producto en inventario no es solo el precio pactado con el proveedor internacional en la orden de compra. A menudo existen **gastos adicionales de adquisición** que llegan días o semanas después:
- Fletes marítimos o terrestres.
- Aranceles e impuestos aduanales no recuperables.
- Honorarios de agentes aduanales y maniobras.
- Seguros de transporte.

El **Landed Cost Voucher (`LandedCostVoucher`)** permite vincular recepciones de compra (`PurchaseReceipt`) ya asentadas, sumar facturas o montos de fletes/aranceles, y prorratear retroactivamente ese costo adicional sobre los productos recibidos, ajustando el valor en libros del inventario y generando el asiento contable compensatorio en el **General Ledger**.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Valuación de Adquisición | `PurchaseReceipt` solo costea al precio unitario pactado | No hay forma de agregar fletes/aranceles posteriores al costo del inventario | Entidad `LandedCostVoucher` que referencia uno o más `PurchaseReceipt` |
| D2 | Estrategias de Prorrateo | No existe motor de prorrateo | Se requiere distribuir los gastos por Cantidad (`Quantity`), por Valor (`Amount`) o por Volumen/Peso | Enumerador `LandedCostDistributeBy` (`Qty`, `Amount`) con cálculo determinista en memoria |
| D3 | Ajuste Contable en General Ledger | Los gastos de flete quedan aislados en cuentas de resultados | El flete debe capitalizarse al activo de inventario contra una cuenta puente de gastos de compra | `LandedCostPosting`: Dr Cuenta de Inventario / Cr Cuenta de Gastos por Aplicar / Fletes |
| D4 | Recálculo de Costo Unitario en Kardex | La tasa de valuación (`ValuationRate`) queda congelada | Se requiere actualizar la tasa unitaria del lote/ítem para que futuras salidas (COGS) lleven el costo real | Generar `StockLedgerEntry` de ajuste de valuación sin mover cantidades físicas |
| D5 | UI de Distribución | Sin pantalla de costeo posterior | Pantalla dedicada con selector de recepciones, captura de cargos y tabla de desglose con "Antes y Después" | Vista `LandedCostVoucherView` y modal `LandedCostVoucherModal` en React 19 |

---

## 2. Escenarios Gherkin (LCV-G1..LCV-G4)

```gherkin
Feature: Landed Cost Voucher Apportionment
  Background:
    Given a submitted Purchase Receipt "PREC-2026-0001" with:
      | Item       | Qty | Unit Price | Total |
      | Insumo A   | 100 | $10.00     | $1,000|
      | Insumo B   |  50 | $20.00     | $1,000|
    And Account "1104" (Inventario de Materias Primas)
    And Account "5105" (Gastos de Flete / Cuenta Puente)

  Scenario: LCV-G1 Prorrateo proporcional por Monto (Amount)
    When creating a Landed Cost Voucher for "PREC-2026-0001"
    And adding a Freight charge of $200 allocated by "Amount"
    Then Insumo A receives $100 additional cost (new valuation rate $11.00/unit)
    And Insumo B receives $100 additional cost (new valuation rate $22.00/unit)

  Scenario: LCV-G2 Prorrateo proporcional por Cantidad (Quantity)
    When creating a Landed Cost Voucher for "PREC-2026-0001"
    And adding a Freight charge of $300 allocated by "Quantity"
    Then Insumo A (100 units) receives $200 additional cost ($2.00/unit)
    And Insumo B (50 units) receives $100 additional cost ($2.00/unit)

  Scenario: LCV-G3 Asiento contable exacto en el General Ledger (DoubleEntryGuard)
    When submitting the Landed Cost Voucher from LCV-G1 ($200)
    Then General Ledger records:
      | Account | Debit  | Credit |
      | 1104    | $200.00| $0.00  | (Incremento valor inventario)
      | 5105    | $0.00  | $200.00| (Absorción de gasto de flete)
    And the voucher status becomes "Submitted"

  Scenario: LCV-G4 Rechazo al referenciar una recepción no confirmada
    When attempting to create a Landed Cost Voucher against a "Draft" or "Cancelled" Purchase Receipt
    Then the operation is rejected with 400 ProblemDetails and code "purchase_receipt_not_submitted"
```

---

## 3. Modelo de Dominio y Contratos (.NET 10)

```csharp
public enum LandedCostDistributeBy
{
    Amount = 1,
    Quantity = 2
}

public class LandedCostVoucher : AggregateRoot<Guid>, ITenantEntity
{
    public string VoucherNo { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public DocumentStatus Status { get; private set; }
    public LandedCostDistributeBy DistributeBy { get; private set; }
    public decimal TotalTaxesAndCharges { get; private set; }
    public byte[] RowVersion { get; private set; }

    public ICollection<LandedCostReceiptReference> Receipts { get; private set; }
    public ICollection<LandedCostChargeItem> TaxesAndCharges { get; private set; }
    public ICollection<LandedCostApportionmentItem> Items { get; private set; }
}

public class LandedCostChargeItem : Entity<Guid>
{
    public string Description { get; private set; }
    public decimal Amount { get; private set; }
    public Guid ExpenseAccountId { get; private set; }
    public Account ExpenseAccount { get; private set; }
}

public class LandedCostApportionmentItem : Entity<Guid>
{
    public Guid PurchaseReceiptId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal Qty { get; private set; }
    public decimal OriginalRate { get; private set; }
    public decimal ApplicableCharges { get; private set; }
    public decimal NewRate { get; private set; }
}
```

---

## 4. Tareas de Implementación

- **T1 (Dominio & DB)**: Crear entidades `LandedCostVoucher`, `LandedCostReceiptReference`, `LandedCostChargeItem`, `LandedCostApportionmentItem`. Migración EF Core `AddLandedCostVoucher`.
- **T2 (Motor de Prorrateo)**: Servicio `ILandedCostCalculationService` para distribuir importes por `Amount` o `Quantity` garantizando balance exacto de centavos.
- **T3 (Posting Contable & Kardex)**: Modificar `StockPostingService.cs` para emitir el asiento de ajuste contable y actualizar la valuación en el Kardex.
- **T4 (Controladores y API)**: `LandedCostVouchersController` con acciones `Create`, `Submit` (idempotente) y `Cancel`.
- **T5 (Frontend UI)**: Crear `LandedCostVoucherView.tsx` y modal de creación con visualización comparativa de tasas de valuación.
