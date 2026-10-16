# Spec — Extensibility Engine: Custom Fields & Document Lifecycle Hooks (Fase 1)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, DDD, .NET 10 DI, JSON Schema, React 19, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

Dotar a **Aspire ERP** de capacidades transversales de extensibilidad para convertirlo en un ERP camaleónico por industria sin introducir condicionales (`if (industry == ...)`) dentro del núcleo:
1. **Custom Fields Engine (`ICustomFieldsCarrier`)**: Permitir que entidades raíz (`Customer`, `Supplier`, `Item`, `Asset`, `SalesOrder`, `SalesInvoice`) almacenen atributos variables validados por JSON Schema persistidos en columna `CustomData` (`NVARCHAR(MAX)` / `JSON`).
2. **Document Lifecycle Hooks (`IDocumentLifecycleHook<T>`)**: Proveer un pipeline abierto mediante Inyección de Dependencias de .NET 10 para ejecutar validaciones o acciones pre/post `Submit` y `Cancel` desde módulos externos o verticales.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Campos Dinámicos | Entidades tipadas rígidamente | No se pueden almacenar datos verticales (ej. No. Póliza en Cliente o VIN en Activo) | Interfaz `ICustomFieldsCarrier` con propiedad `CustomData` (string JSON) + catálogo `CustomFieldDefinition` |
| D2 | Validación de Esquema | Validación solo por FluentValidation en DTOs estáticos | Los campos dinámicos podrían guardar JSON corrupto o incompleto | Middleware/Service de validación con JSON Schema antes de persistir comandos |
| D3 | Pipeline de Intercepción | Handlers invocan métodos directos (`invoice.Submit()`) | Módulos satélite no pueden validar ni disparar efectos secundarios sin modificar el Core | `IEnumerable<IDocumentLifecycleHook<T>>` inyectado en Command Handlers con ejecución ordenada por propiedad `Order` |
| D4 | Frontend Dinámico | Formularios fijos con inputs cableados en TSX | No hay renderizado de campos adicionales según la entidad/tenant | Componente `<CustomFieldsRenderer />` en React 19 basado en la definición de campos del backend |
| D5 | Errores / RFC 7807 | Códigos fijos en backend | Falta manejar violaciones de esquema o bloqueos de hooks externos | Nuevos códigos: `custom_field_validation_failed`, `lifecycle_hook_rejected` mapeados a ProblemDetails |

---

## 2. Escenarios Gherkin (EXT-G1..EXT-G4)

```gherkin
Feature: Extensibility Engine - Custom Fields and Lifecycle Hooks
  Background:
    Given Tenant "T1" has configured a Custom Field "bloodType" (string, required) for entity "Customer"

  Scenario: EXT-G1 Persistencia y validación exitosa de Custom Fields
    When creating a Customer with CustomData '{"bloodType": "O+"}'
    Then the Customer is created with status 201
    And the CustomData contains the key "bloodType" with value "O+"

  Scenario: EXT-G2 Rechazo por violación de JSON Schema en Custom Fields
    When creating a Customer with CustomData '{"bloodType": 123}'
    Then the creation fails with 400 ProblemDetails and code "custom_field_validation_failed"

  Scenario: EXT-G3 Interceptor de ciclo de vida bloquea Submit
    Given an external hook registered for SalesInvoice that checks insurance authorization
    When submitting a SalesInvoice without insurance authorization
    Then the hook aborts submission with 422 ProblemDetails and code "insurance_authorization_required"
    And no General Ledger entries are posted

  Scenario: EXT-G4 Ejecución ordenada de Hooks
    Given Hook A with Order 10 and Hook B with Order 20
    When submitting a document
    Then Hook A executes before Hook B
```

---

## 3. Contratos de Dominio (.NET 10)

```csharp
namespace Erp.Domain.Common;

public interface ICustomFieldsCarrier
{
    string? CustomData { get; set; }
}

public interface IDocumentLifecycleHook<in TDocument> where TDocument : class
{
    int Order => 0;
    Task OnBeforeSubmitAsync(TDocument document, CancellationToken ct);
    Task OnAfterSubmitAsync(TDocument document, CancellationToken ct);
    Task OnBeforeCancelAsync(TDocument document, CancellationToken ct);
    Task OnAfterCancelAsync(TDocument document, CancellationToken ct);
}
```

---

## 4. Tareas de Implementación

- **T1 (Dominio & Entidades)**: Crear `ICustomFieldsCarrier` e implementarla en `Customer`, `Supplier`, `Item`, `Asset`, `SalesInvoice`, `PurchaseInvoice`.
- **T2 (Infraestructura EF Core)**: Configurar mapeo de columna `CustomData` (`NVARCHAR(MAX)` / `JSON`) en configuraciones EF Core existentes. Crear migración `AddCustomFieldsSupport`.
- **T3 (Servicio de Validación JSON Schema)**: Crear `ICustomFieldValidator` y catálogo en base de datos `CustomFieldDefinition` (`EntityName`, `FieldName`, `FieldType`, `IsRequired`, `ValidationSchemaJson`).
- **T4 (Pipeline de Hooks en Application)**: Modificar `SubmitSalesInvoiceCommandHandler`, `SubmitPurchaseInvoiceCommandHandler` y `Cancel*` para inyectar y ejecutar `IEnumerable<IDocumentLifecycleHook<T>>`.
- **T5 (Frontend React 19)**: Crear componente `<CustomFieldsRenderer />` y hook `useCustomFields(entityType)` para renderizar inputs dinámicos en los formularios de creación/edición.
