# Spec — Batch & Serial Number Tracking in Stock / Logistics (Fase 3)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, Strict Kardex Invariants, DoubleEntryGuard, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

Actualmente el módulo de inventario (`Stock`) controla existencias agregadas por `(ItemId, WarehouseId)`.

Para habilitar industrias con requerimientos estrictos de trazabilidad:
1. **Farmacéutica, Alimentos, Químicos y Agro**: Requieren control de **Lotes (`Batch`)** con fecha de fabricación, fecha de caducidad y estrategia de despacho **FEFO** (First Expired, First Out).
2. **Electrónica, Maquinaria, Automotriz y Médico**: Requieren **Números de Serie (`SerialNo`)** únicos por pieza para garantías, servicio técnico y trazabilidad de origen.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Configuración de Trazabilidad | `Item` no sabe si requiere lote o serie | Se requiere indicar por producto si se gestiona por lote (`HasBatchNo`) o por serie (`HasSerialNo`) | Flags booleanos en `Item` + validación de exclusividad o coexistencia |
| D2 | Entidades Maestras de Trazabilidad | No existen `Batch` ni `SerialNumber` | No se pueden almacenar fechas de caducidad ni estatus de series | Nuevas entidades `Batch` (con `ExpiryDate`, `BatchNo`) y `SerialNumber` (con `SerialNo`, `Status`) ligadas a `Item` |
| D3 | Movimientos de Kardex (`StockEntryLine`) | Solo guarda `ItemId`, `WarehouseId`, `Qty`, `ValuationRate` | No se sabe qué lote o series específicas entraron o salieron | Campos `BatchId` y colección de `SerialNumbers` en líneas de movimiento de inventario, compras y entregas |
| D4 | Validación de Existencias por Lote/Serie | Solo valida saldo global del almacén | Podría despacharse un lote agotado, un lote caducado o una serie que no existe en el almacén | `StockValidator` verifica saldo suficiente por lote y que cada serie esté en el almacén de origen |
| D5 | UI Selector de Lotes y Series | Inputs de cantidad simples | Selector de lotes disponibles con indicación de caducidad y modal de escaneo/captura de series | Componentes `<BatchSelector />` y `<SerialNoPickerModal />` en React 19 |

---

## 2. Escenarios Gherkin (STK-TRK-G1..STK-TRK-G4)

```gherkin
Feature: Batch and Serial Number Tracking
  Background:
    Given an Item "Vacuna Antigripal" with hasBatchNo true and hasSerialNo false
    And an Item "Laptop Ultrabook" with hasBatchNo false and hasSerialNo true
    And Warehouse "Almacén Central"

  Scenario: STK-TRK-G1 Recepción y creación de Lote con caducidad
    When receiving 100 units of "Vacuna Antigripal" with Batch "LOT-2026-001" and expiry "2027-12-31"
    Then the Batch record is created with quantity 100 in "Almacén Central"
    And the stock ledger records the movement with batch reference

  Scenario: STK-TRK-G2 Despacho con validación de Lote y bloqueo de caducados
    Given Batch "LOT-EXPIRED" expired on "2026-01-01" with 10 units in stock
    When attempting a DeliveryNote or StockEntry issue with Batch "LOT-EXPIRED"
    Then the operation is rejected with 400 ProblemDetails and code "batch_has_expired"

  Scenario: STK-TRK-G3 Control estricto de Números de Serie únicos
    When receiving 2 units of "Laptop Ultrabook" with serials "SN-1001" and "SN-1002"
    Then 2 SerialNumber records are created in status "Active" in "Almacén Central"
    When attempting to deliver 1 unit with serial "SN-9999" (not in warehouse)
    Then the delivery is rejected with 400 ProblemDetails and code "serial_not_in_warehouse"

  Scenario: STK-TRK-G4 Trazabilidad en Kardex y Reversión
    Given a submitted StockEntry issuing serial "SN-1001"
    When cancelling the StockEntry
    Then the serial "SN-1001" status reverts back to "Active" in "Almacén Central"
```

---

## 3. Modelo de Dominio y Contratos (.NET 10)

```csharp
public class Batch : AggregateRoot<Guid>, ITenantEntity
{
    public Guid ItemId { get; private set; }
    public Item Item { get; private set; }
    public string BatchNo { get; private set; }
    public DateOnly? ManufacturingDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public bool IsDisabled { get; private set; }
}

public class SerialNumber : AggregateRoot<Guid>, ITenantEntity
{
    public Guid ItemId { get; private set; }
    public Item Item { get; private set; }
    public string SerialNo { get; private set; }
    public Guid? CurrentWarehouseId { get; private set; }
    public SerialStatus Status { get; private set; } // InStock, Delivered, Scrapped
}
```

---

## 4. Tareas de Implementación

- **T1 (Dominio & DB)**: Agregar flags `HasBatchNo` y `HasSerialNo` a `Item`. Crear entidades `Batch`, `SerialNumber` y migración EF Core `AddBatchAndSerialTracking`.
- **T2 (Líneas de Kardex)**: Modificar `StockEntryLine`, `DeliveryNoteLine`, `PurchaseReceiptLine` para incluir `BatchId` y `SerialNumbers`.
- **T3 (Validaciones de Almacén)**: Enriquecer `StockValidator` para auditar disponibilidad por lote, fechas de caducidad e integridad de series únicas.
- **T4 (Controladores y API)**: Endpoints para consulta de lotes disponibles por almacén (`GET /api/v1/batches?itemId=...&warehouseId=...`) y verificación de series.
- **T5 (Frontend UI)**: Componentes `<BatchSelector />` y `<SerialNoPickerModal />` para la captura y selección en movimientos de inventario y remisiones.
