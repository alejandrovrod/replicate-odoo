# Spec — TaxEngine: Multi-Tier Taxes, Retentions & Compound Rates (Fase 2)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean/Hexagonal Architecture, DoubleEntryGuard, Strict GL Invariants, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

Actualmente la facturación de ventas y compras en Aspire ERP asume un esquema impositivo básico (un único porcentaje/monto de IVA).

Para operar en múltiples industrias y esquemas fiscales (honorarios médicos, constructoras, servicios profesionales, fletes, comercio con retenciones gubernamentales o corporativas, e impuestos en cascada como IEPS):
1. **Plantillas de Impuestos Reutilizables (`SalesTaxTemplate` y `PurchaseTaxTemplate`)**: Catálogo maestro de paquetes de impuestos preconfigurados (ej. "Honorarios Profesionales: IVA 16% + Retención ISR 10% + Retención IVA 10.667%").
2. **Líneas de Impuesto por Documento (`InvoiceTaxLine`)**:
   - Soporte para tipo de impuesto: **Impuesto Trasladado/Adicional** (`Add`) o **Retención** (`Deduct`).
   - Base de cálculo: Sobre Monto Neto (`OnNetTotal`), Sobre Impuesto Previo (`OnPreviousRow` para impuestos compuestos o cascada).
   - Asignación obligatoria de `AccountId` en el General Ledger para cada componente tributario.
3. **Integración con Asiento Contable (`DoubleEntryGuard`)**: El asiento contable desglosa con exactitud las cuentas de pasivo (IVA por pagar), activo (IVA acreditable) y cuentas de retención (Retenciones por enterar o retenciones a favor) manteniendo $\sum \text{Débito} = \sum \text{Crédito}$.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Plantillas Maestras | No existen plantillas; solo tasas quemadas en lógica | Se requiere configurar paquetes de impuestos por tenant y país | Entidades `TaxTemplate` y `TaxTemplateLine` (con tipo, tasa/monto y cuenta GL) |
| D2 | Retenciones e Impuestos Compuestos | Solo suma de impuestos positivos | No es posible restar retenciones al total a pagar sin desbalancear el GL | `TaxLine` soporta `TaxChargeType` (`Add` | `Deduct`) y cálculo sobre fila previa |
| D3 | GL Posting en Facturas | `SalesPosting` / `PurchasePosting` asumen una sola cuenta fiscal | Deben acreditar/debitar tantas cuentas contables como líneas de impuesto existan | Iterar sobre `Invoice.TaxLines` para construir los `LedgerEntry` correspondientes |
| D4 | Total Documental y Saldo | `TotalAmount = SubTotal + Tax` | Con retenciones: `TotalAmount = SubTotal + Taxes - Retentions` (determina `OutstandingAmount`) | Invariante matemática explícita en `SalesInvoice` y `PurchaseInvoice` |
| D5 | UI Selector & Desglose | Input numérico simple de IVA | Selector de plantilla con recálculo automático y desglose en el pie de factura | Componente `<TaxBreakdownTable />` en modales de factura de venta y compra |

---

## 2. Escenarios Gherkin (TAX-G1..TAX-G4)

```gherkin
Feature: Multi-Tier Tax Engine & Retentions
  Background:
    Given configured accounts: "4101" (Ventas), "1105" (Clientes), "2108" (IVA Trasladado), "2109" (Retención ISR), "2110" (Retención IVA)
    And a Sales Tax Template "Honorarios PM a PF" with:
      | Type   | Rate   | Account |
      | Add    | 16.00% | 2108    |
      | Deduct | 10.00% | 2109    |
      | Deduct | 10.667%| 2110    |

  Scenario: TAX-G1 Cálculo y desglose con retenciones
    When creating a Sales Invoice with subtotal $10,000 applying template "Honorarios PM a PF"
    Then the calculated IVA is $1,600
    And the calculated Retención ISR is $1,000
    And the calculated Retención IVA is $1,066.70
    And the net payable amount (TotalAmount) is $9,533.30

  Scenario: TAX-G2 Contabilización exacta en el General Ledger (DoubleEntryGuard)
    When submitting the previous Sales Invoice
    Then the General Ledger records:
      | Account | Debit     | Credit    |
      | 1105    | $9,533.30 | $0.00     |
      | 2109    | $1,000.00 | $0.00     | (Retención a favor)
      | 2110    | $1,066.70 | $0.00     | (Retención a favor)
      | 4101    | $0.00     | $10,000.00|
      | 2108    | $0.00     | $1,600.00 |
    And the transaction balance is strictly zero

  Scenario: TAX-G3 Impuesto compuesto / en cascada (OnPreviousRow)
    Given an excise tax (IEPS) of 8% on net, and IVA of 16% calculated on (Net + IEPS)
    When invoicing an item of $1,000
    Then IEPS is $80.00
    And IVA is 16% of $1,080.00 = $172.80
    And total tax is $252.80

  Scenario: TAX-G4 Rechazo de plantilla sin cuenta contable asignada
    When attempting to save a TaxTemplateLine without a valid GL AccountId
    Then the operation is rejected with 400 ProblemDetails and code "tax_account_required"
```

---

## 3. Modelo de Dominio y Contratos (.NET 10)

```csharp
public enum TaxChargeType
{
    Add = 1,     // Traslado / Impuesto adicional (ej. IVA, IEPS)
    Deduct = 2   // Retención (ej. Retención ISR, Retención IVA)
}

public enum TaxCalculationBasis
{
    OnNetTotal = 1,
    OnPreviousRow = 2,
    FixedAmount = 3
}

public class TaxTemplate : AggregateRoot<Guid>, ITenantEntity
{
    public string Name { get; private set; }
    public string Category { get; private set; } // Sales or Purchase
    public bool IsDefault { get; private set; }
    public ICollection<TaxTemplateLine> Lines { get; private set; }
}

public class InvoiceTaxLine : Entity<Guid>
{
    public string Title { get; private set; }
    public TaxChargeType ChargeType { get; private set; }
    public TaxCalculationBasis CalculationBasis { get; private set; }
    public decimal Rate { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal BaseAmount { get; private set; }
    public Guid AccountId { get; private set; }
    public Account Account { get; private set; }
}
```

---

## 4. Tareas de Implementación

- **T1 (Dominio & DB)**: Crear entidades `TaxTemplate`, `TaxTemplateLine` y colección `TaxLines` en `SalesInvoice` y `PurchaseInvoice`. Migración EF Core `AddTaxEngineAndTemplates`.
- **T2 (Motor de Cálculo)**: Implementar `ITaxEngineService` con cálculo puro en memoria para netos, retenciones y cascada.
- **T3 (Posting Contable GL)**: Modificar `SalesPosting.cs` y `PurchasePosting.cs` para emitir las líneas contables debitando o acreditando las cuentas de retención e impuesto según corresponda.
- **T4 (API & Endpoints)**: Controladores para catálogos de plantillas (`TaxTemplatesController`) y endpoints de vista previa de cálculo de impuestos.
- **T5 (Frontend UI)**: Componente `<TaxBreakdownTable />` integrado en los modales de Factura de Venta y Factura de Compra con selector de plantilla y visualización en tiempo real.
