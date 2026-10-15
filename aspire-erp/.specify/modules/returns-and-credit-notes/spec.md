# Spec — Sales & Purchase Returns / Credit Notes (Notas de Crédito y Devoluciones)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean/Hexagonal Architecture, Double Entry Guard, Fiscal Invariants, RFC 7807**

---

## 0. Propósito

Manejo integral de devoluciones de mercancía y ajustes de saldo mediante Notas de Crédito / Facturas de Retorno para clientes y proveedores:
1. **Devolución de Ventas (Credit Note / Sales Return)**: Reversión parcial o total del saldo por cobrar al cliente (`Accounts Receivable`), ajuste proporcional de impuestos (IVA trasladado), y opción de reingreso físico a almacén de inventario con costeo promedio ponderado o FIFO.
2. **Devolución de Compras (Debit Note / Purchase Return)**: Reversión de cuentas por pagar al proveedor (`Accounts Payable`), ajuste de IVA acreditable y salida física de almacén.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Entidad de Retorno | Solo existen `SalesInvoice` y `PurchaseInvoice` estándar | Facturas no soportan cantidades negativas ni reversión de inventario asociada | Diseñar `SalesInvoice` con bandera `IsReturn = true` y `ReturnAgainstInvoiceId` obligatorio en retornos (patrón ERPNext) |
| D2 | Inventario / Stock Ledger | Salidas manuales o automáticas al facturar | Al devolver mercancía debe generarse un `StockEntry` tipo `Sales Return` o `Purchase Return` | Transacción atómica: si `UpdateStock = true`, se crea y valida el movimiento en el Ledger de Stock |
| D3 | Conciliación de Saldos | El outstanding de la factura original queda intacto | Una nota de crédito debe amortizar o reducir el `OutstandingAmount` de la factura de origen | Al someter la Nota de Crédito, se descuenta directamente de `OutstandingAmount` de la factura referenciada |
| D4 | Asiento Contable (GL) | Solo asientos de venta estándar | Asiento inverso estricto: Dr Devoluciones sobre ventas, Dr IVA por trasladar / Cr Cuentas por Cobrar | El posting builder intercambia las naturalezas asegurando balance exacto |

---

## 2. Escenarios Gherkin (RET-G1..RET-G4)

```gherkin
Feature: Sales & Purchase Returns / Credit Notes
  Background:
    Given a Customer "Comercializadora del Norte"
    And a submitted Sales Invoice "INV-2026-0001" with total 1160 (1000 base + 160 IVA) and 1 item "Teclado Mecánico" qty 2

  Scenario: RET-G1 Nota de Crédito con devolución de stock
    When creating a Credit Note against "INV-2026-0001" for 1 qty of "Teclado Mecánico" with updateStock true
    And submitting the Credit Note
    Then status becomes "Submitted" with voucher starting with "RET-INV-"
    And the Sales Invoice "INV-2026-0001" OutstandingAmount is reduced by 580
    And General Ledger posts Dr Sales Return 500, Dr Sales Tax 80, Cr Accounts Receivable 580
    And a Stock Ledger entry adds 1 qty back to the source Warehouse

  Scenario: RET-G2 Nota de Crédito por ajuste de precio (sin movimiento de inventario)
    When creating a Credit Note against "INV-2026-0001" for amount 200 with updateStock false
    And submitting the Credit Note
    Then General Ledger posts Dr Discount/Adjustment, Dr Tax, Cr Receivable
    And no Stock Ledger entry is generated

  Scenario: RET-G3 Intento de devolver más cantidad o monto que la factura original
    When creating a Credit Note against "INV-2026-0001" requesting qty 3
    Then submission is rejected with 400 ProblemDetails and code "return_quantity_exceeds_original"

  Scenario: RET-G4 Cancelación de Nota de Crédito
    Given a submitted Credit Note
    When cancelling the Credit Note
    Then the original Sales Invoice OutstandingAmount is restored
    And reversing GL lines are posted
    And returned stock is removed from Warehouse
```

---

## 3. Modelo de Dominio y Validaciones

```csharp
public class SalesInvoice : AggregateRoot<Guid>, ITenantEntity
{
    // ... campos existentes ...
    public bool IsReturn { get; private set; }
    public Guid? ReturnAgainstInvoiceId { get; private set; }
    public bool UpdateStock { get; private set; }

    public void ValidateReturnInvariants(SalesInvoice originalInvoice)
    {
        if (!IsReturn) return;
        if (originalInvoice == null)
            throw new SalesValidationException("return_target_invoice_required", "Return must reference an existing invoice.");
        if (originalInvoice.Status != DocumentStatus.Submitted)
            throw new SalesValidationException("return_target_not_submitted", "Cannot return against unsubmitted invoice.");
        
        // Validar cantidades no excedentes contra retornos previos
    }
}
```

---

## 4. Tareas de Implementación

- **T1**: Migración BD: `IsReturn`, `ReturnAgainstInvoiceId`, `UpdateStock` en `SalesInvoices` y `PurchaseInvoices`.
- **T2**: Invariantes de Dominio y Cálculo de saldo pendiente en `SalesInvoice` y `PurchaseInvoice`.
- **T3**: Modificar `SalesPosting.cs` y `PurchasePosting.cs` para generar asientos inversos.
- **T4**: Integrar con `StockLedgerService` para reversión de inventarios al someter/cancelar retornos.
- **T5**: Frontend: Botón "Crear Devolución / Nota de Crédito" en vista de factura, modal selector de ítems a devolver, visualización de saldo ajustado.
