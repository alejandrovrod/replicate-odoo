# Spec — Sales Invoice ERPNext Parity (Direct Stock & Advances Allocation)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, ERPNext Parity, Strict Inventory Valuation, DoubleEntryGuard, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

En ERPNext (`accounts/doctype/sales_invoice`), la **Factura de Venta** no solo genera el asiento contable de cuentas por cobrar e ingresos, sino que integra dos flujos operativos clave para distribuidoras, comercios y empresas de servicios:
1. **Facturación con entrega inmediata (`Update Stock`):** Permite rebajar inventario físico en el momento de emitir la factura, generando el asiento simultáneo de Costo de Ventas (COGS) e Inventario sin obligar al usuario a crear un Remito (`DeliveryNote`) por separado.
2. **Cruce de Anticipos (`Advances Allocation`):** Permite cruzar cobros o pagos anticipados ya asentados del cliente (`PaymentEntry`), reduciendo automáticamente el saldo pendiente (`OutstandingAmount`) y evitando cobros duplicados.
3. **Planes de Vencimiento (`Payment Schedule`):** Soporta términos de pago escalonados con múltiples cuotas y fechas de vencimiento.

---

## 1. Gap Matrix (Actual vs ERPNext)

| # | Dimensión | Estado Actual en Aspire ERP | Paridad ERPNext a Implementar | Decisión Arquitectónica |
|---|-----------|-----------------------------|-------------------------------|--------------------------|
| D1 | Movimiento de Stock Directo | Solo factura contable; el stock requiere `DeliveryNote` | Switch `UpdateStock` con selector obligatorio de `WarehouseId` | Si `UpdateStock = true`, al hacer `Submit` se generan simultáneamente los `StockLedgerEntry` (FIFO) y el asiento `Dr Costo de Ventas / Cr Inventario`. |
| D2 | Cruce de Anticipos | Factura nace con `OutstandingAmount = GrandTotal` | Tabla `SalesInvoiceAdvance`: cruce de anticipos de `PaymentEntry` | `Submit` aplica el anticipo, asienta el asiento contable de compensación (`Dr Anticipos de Clientes / Cr Cuentas por Cobrar`) y reduce `OutstandingAmount`. |
| D3 | Términos de Pago | Un solo campo `DueDate` en cabecera | Tabla `SalesInvoicePaymentSchedule` con cuotas y porcentajes | Tabla hija en factura con validación de suma de cuotas = `GrandTotal`. |
| D4 | UI Modal Factura | Formulario lineal sin selección de almacén ni anticipos | Formulario ampliado con pestaña/sección de `Entrega Inmediata` y modal de `Asignar Anticipos` | Interfaz reactiva en React 19 con validación de saldo disponible del anticipo. |

---

## 2. Escenarios Gherkin

```gherkin
Feature: Sales Invoice ERPNext Parity (Update Stock & Advances)
  Background:
    Given an active Company "COMP-001"
    And Customer "CUST-001" with Receivable Account "1103" and Advance Account "2105"
    And Item "IT-001" with Inventory Account "1104" and Cost of Goods Sold Account "5101"
    And Warehouse "WH-MAIN" with 10 units of "IT-001" valued at $50/unit

  Scenario: SI-G1 Factura de venta con entrega inmediata (Update Stock)
    When creating and submitting a Sales Invoice for "CUST-001" with:
      | Item | Qty | Rate | UpdateStock | Warehouse |
      | IT-001 | 2 | $100 | true | WH-MAIN |
    Then General Ledger records:
      | Account | Debit | Credit |
      | 1103 (CxC) | $200.00 | $0.00 |
      | 4101 (Ingresos) | $0.00 | $200.00 |
      | 5101 (COGS) | $100.00 | $0.00 |
      | 1104 (Inventario) | $0.00 | $100.00 |
    And Stock Ledger records -2 units in "WH-MAIN" for "IT-001"

  Scenario: SI-G2 Facturación con cruce de anticipo previo
    Given an unallocated Payment Entry "PAY-001" of $150 from "CUST-001"
    When submitting a Sales Invoice for "CUST-001" with Grand Total of $200
    And allocating $150 from "PAY-001"
    Then the invoice Outstanding Amount becomes $50.00
    And General Ledger records:
      | Account | Debit | Credit |
      | 2105 (Anticipos de Clientes) | $150.00 | $0.00 |
      | 1103 (Cuentas por Cobrar) | $0.00 | $150.00 |

  Scenario: SI-G3 Rechazo al cruzar más anticipo que el total de la factura
    Given an unallocated Payment Entry "PAY-001" of $500
    When attempting to allocate $500 to a Sales Invoice of $200
    Then the operation is rejected with 422 ProblemDetails and code "ADVANCE_EXCEEDS_INVOICE_TOTAL"
```

---

## 3. Modelo de Dominio (.NET 10 / C#)

```csharp
public class SalesInvoice : AggregateRoot<Guid>, ITenantEntity
{
    public Guid CompanyId { get; private set; }
    public string InvoiceNumber { get; private set; } = null!;
    public Guid CustomerId { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DocumentStatus Status { get; private set; }
    
    // Paridad ERPNext: Update Stock
    public bool UpdateStock { get; private set; }
    public Guid? SourceWarehouseId { get; private set; }

    // Totales
    public decimal NetTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }
    public decimal TotalAdvance { get; private set; }
    public decimal OutstandingAmount { get; private set; }

    private readonly List<SalesInvoiceItem> _items = new();
    public IReadOnlyCollection<SalesInvoiceItem> Items => _items.AsReadOnly();

    private readonly List<SalesInvoiceAdvance> _advances = new();
    public IReadOnlyCollection<SalesInvoiceAdvance> Advances => _advances.AsReadOnly();

    public byte[] RowVersion { get; private set; } = null!;

    public void AllocateAdvance(Guid paymentEntryId, decimal allocatedAmount)
    {
        if (Status != DocumentStatus.Draft)
            throw new InvalidOperationException("Advances can only be allocated on draft invoices.");

        if (TotalAdvance + allocatedAmount > GrandTotal)
            throw new DomainValidationException("ADVANCE_EXCEEDS_INVOICE_TOTAL", "Allocated advance cannot exceed grand total.");

        _advances.Add(new SalesInvoiceAdvance(Id, paymentEntryId, allocatedAmount));
        TotalAdvance += allocatedAmount;
        OutstandingAmount = GrandTotal - TotalAdvance;
    }
}

public class SalesInvoiceAdvance : Entity<Guid>
{
    public Guid SalesInvoiceId { get; private set; }
    public Guid PaymentEntryId { get; private set; }
    public decimal AllocatedAmount { get; private set; }

    public SalesInvoiceAdvance(Guid salesInvoiceId, Guid paymentEntryId, decimal allocatedAmount)
    {
        SalesInvoiceId = salesInvoiceId;
        PaymentEntryId = paymentEntryId;
        AllocatedAmount = allocatedAmount;
    }
}
```

---

## 4. API Boundaries & RFC 7807 Errors

### Endpoints
- `GET /api/v1/sales-invoices/{id}/eligible-advances`: Lista pagos anticipados no conciliados del cliente.
- `POST /api/v1/sales-invoices`: Acepta `updateStock`, `sourceWarehouseId` y arreglo de `advances`.
- `POST /api/v1/sales-invoices/{id}/submit`: Asienta contabilidad, Kardex (si `updateStock = true`) y cruce de anticipos.

### Problem Details Error Codes
- `UPDATE_STOCK_WAREHOUSE_REQUIRED` (400 Bad Request)
- `INSUFFICIENT_STOCK_FOR_INVOICE` (422 Unprocessable Entity)
- `ADVANCE_EXCEEDS_INVOICE_TOTAL` (422 Unprocessable Entity)
- `PAYMENT_ENTRY_NOT_FOUND_OR_DEPLETED` (404 / 409 Conflict)
