# Spec — Workflow Engine & Industry Presets with Terminology Overlay (Fase 5)

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, Decoupled State Machine, Tenant Terminology Overlay, RFC 7807**

---

## 0. Propósito y Contexto de Negocio

Esta fase corona la arquitectura camaleónica de **Aspire ERP**:
1. **Desacoplamiento `WorkflowState` vs `DocStatus`**:
   - `DocStatus` (0 = Draft, 1 = Submitted, 2 = Cancelled) permanece como el **contrato sagrado e inviolable** del General Ledger y del Kardex.
   - `WorkflowState` introduce una máquina de estados operativa configurable por documento y por tenant (ej. para un taller: `Diagnóstico` → `En Espera de Piezas` → `Presupuesto Aprobado` → `Listo para Facturar`), donde solo estados operativos específicos tienen autorización para disparar la transición a `Submitted`.
2. **Industry Presets (`VerticalPreset`)**:
   - Paquetes de configuración semilla por industria (ej. `healthcare`, `automotive`, `construction`, `distribution`) que en segundos configuran:
     - Campos personalizados iniciales (`CustomFieldDefinition`).
     - Máquinas de estado documental (`WorkflowDefinition`).
     - Plantillas fiscales (`TaxTemplate`).
3. **Tenant Terminology Overlay (i18n dinámico)**:
   - Capa de sobreescritura de vocabulario en frontend que sustituye términos globales por los del rubro sin alterar el wire-contract ni las entidades del backend (ej. en salud: `Customer` → `Paciente`, `SalesOrder` → `Orden de Admisión`; en educación: `Customer` → `Alumno`).

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Gap a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|-------------------|--------------------------|
| D1 | Estados Operativos | Los documentos saltan directamente de `Draft` a `Submitted` | Se requieren aprobaciones multinivel y estados operativos previos | Entidades `WorkflowDefinition`, `WorkflowTransition` y propiedad `WorkflowState` en entidades documentales |
| D2 | Gatekeeper de Contabilización | Cualquier usuario con permiso puede hacer `Submit` | Solo el estado final del flujo operativo debe permitir la contabilización contable | `WorkflowGuard`: si un documento tiene workflow activo, `Submit` solo procede si `WorkflowState` es un estado terminal autorizado |
| D3 | Presets de Industria | Configuración manual tabla por tabla | Se requiere desplegar una vertical completa en un solo clic | Semillero `VerticalPresetService` con soporte para archivos JSON por industria |
| D4 | Vocabulario de Industria | Traducciones estáticas ("Cliente", "Proveedor", "Orden") | Una clínica lee "Paciente", una constructora "Obra" | Capa interceptora en `i18n` de React que consulta un diccionario `TerminologyOverlay` por tenant |
| D5 | UI de Transición de Estados | Solo botones "Guardar Borrador" y "Confirmar" | Barra de acciones dinámica con botones según las transiciones permitidas por el rol del usuario | Componente `<WorkflowActionBar />` en cabeceras de documentos |

---

## 2. Escenarios Gherkin (WF-PRESET-G1..WF-PRESET-G4)

```gherkin
Feature: Workflow State Machine and Industry Presets
  Background:
    Given Tenant "Clinica Santa Maria" configured with Industry Preset "healthcare"
    And a Workflow for "SalesOrder" with states:
      | State       | IsInitial | CanSubmit | AllowedRoles  |
      | Admision    | true      | false     | Recepcion     |
      | Consulta    | false     | false     | Medico        |
      | Autorizado  | false     | true      | Administracion|

  Scenario: WF-PRESET-G1 Bloqueo de Submit en estado operativo no terminal
    Given a SalesOrder in state "Admision"
    When attempting to Submit the SalesOrder
    Then the action is rejected with 400 ProblemDetails and code "workflow_submit_not_allowed"

  Scenario: WF-PRESET-G2 Transición autorizada y Submit exitoso
    When transitioning the SalesOrder from "Admision" to "Consulta" by role "Recepcion"
    And transitioning from "Consulta" to "Autorizado" by role "Medico"
    And submitting the SalesOrder by role "Administracion"
    Then the SalesOrder DocStatus becomes "Submitted"
    And General Ledger posting proceeds normally

  Scenario: WF-PRESET-G3 Despliegue de Preset por Industria (Healthcare)
    When applying preset "healthcare" to a new tenant
    Then Custom Fields for Customer ("bloodType", "insurancePolicy") are automatically seeded
    And Terminology Overlay maps "selling.customer.singular" to "Paciente"

  Scenario: WF-PRESET-G4 Terminology Overlay en Frontend
    When the user navigates to the selling module under tenant "Clinica Santa Maria"
    Then the navigation title displays "Pacientes" instead of "Clientes"
    And the backend API continues exchanging "CustomerId" without breaking wire contract
```

---

## 3. Modelo de Dominio y Contratos (.NET 10)

```csharp
public class WorkflowDefinition : AggregateRoot<Guid>, ITenantEntity
{
    public string DocumentType { get; private set; } // e.g. "SalesOrder", "PurchaseInvoice"
    public string Name { get; private set; }
    public bool IsActive { get; private set; }
    public ICollection<WorkflowStateDefinition> States { get; private set; }
    public ICollection<WorkflowTransition> Transitions { get; private set; }
}

public class WorkflowStateDefinition : Entity<Guid>
{
    public string StateName { get; private set; }
    public bool IsInitial { get; private set; }
    public bool CanSubmit { get; private set; } // Solo en este estado se autoriza DocStatus = Submitted
}

public class WorkflowTransition : Entity<Guid>
{
    public string FromState { get; private set; }
    public string ToState { get; private set; }
    public string ActionLabel { get; private set; }
    public string AllowedRole { get; private set; }
}

public interface IWorkflowCarrier
{
    string? WorkflowState { get; set; }
}
```

---

## 4. Tareas de Implementación

- **T1 (Dominio & DB)**: Crear entidades `WorkflowDefinition`, `WorkflowStateDefinition`, `WorkflowTransition`. Implementar `IWorkflowCarrier` en `SalesOrder`, `SalesInvoice`, `PurchaseOrder`, `PurchaseInvoice`. Migración EF Core `AddWorkflowEngineAndPresets`.
- **T2 (Guardia de Flujo en Aplicación)**: Servicio `IWorkflowEngineService` y validador `WorkflowGuard` que intercepta `OnBeforeSubmitAsync` (vía `IDocumentLifecycleHook<T>`) para bloquear el `Submit` si el documento no está en un estado con `CanSubmit == true`.
- **T3 (Servicio de Presets)**: `VerticalPresetService` que carga y aplica presets JSON (`healthcare.json`, `automotive.json`, `distribution.json`).
- **T4 (Controladores y API)**: `WorkflowDefinitionsController` y `VerticalPresetsController` (`POST /api/v1/presets/apply`).
- **T5 (Frontend UI & i18n Overlay)**:
  - Crear `<WorkflowActionBar />` con botones de acción según las transiciones disponibles.
  - Implementar `TerminologyOverlayProvider` en React 19 que sobreescribe claves de `i18next` dinámicamente según el preset del tenant.
