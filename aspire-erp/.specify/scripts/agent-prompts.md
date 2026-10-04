# Plantillas Oficiales de Agentes para Aspire ERP

Este documento contiene los prompts estandarizados para invocar a los agentes especializados sobre los distintos proyectos de la solución, garantizando el cumplimiento de las especificaciones y buenas prácticas.

## 1. Auditoría e Implementación de Backend
**Agentes requeridos:** `@dotnet-core-expert` y `@database-optimizer`

```markdown
Actúa conjuntamente como `@dotnet-core-expert` y `@database-optimizer` para realizar una revisión técnica de la solución de backend en `aspire-erp/src/Backend/` y luego implementar mejoras críticas de concurrencia.

### FASE 1: Auditoría Técnica (Solo Lectura)
1. **Arquitectura y Diseño (.NET 10 & C# 14):**
   - Evaluar la separación de responsabilidades entre `Erp.Domain`, `Erp.Application`, `Erp.Infrastructure`, `Erp.Api` y `Erp.AppHost`.
   - Verificar patrones de Clean Architecture, CQRS, handlers, validaciones y tratamiento de errores/result patterns.
   - Analizar el aprovechamiento de C# 14 / .NET 10 (record types, pattern matching, global usings, nullable reference types, AOT-readiness).

2. **Cumplimiento de Especificaciones:**
   - Contrastar los endpoints, entidades y comandos existentes contra los requerimientos certificados en `aspire-erp/.specify/modules/` (módulos 01 a 09).
   - Generar un breve informe de brechas de implementación (Módulos implementados vs. Faltantes).

### FASE 2: Implementación de Concurrencia Optimista (`RowVersion`)
Para cumplir con los módulos `01-accounting` y `02-stock`, necesitamos proteger la persistencia contra condiciones de carrera:

1. **Dominio (`Erp.Domain\Entities\`):** 
   Inyectar la propiedad `public byte[] RowVersion { get; set; }` en todas las entidades transaccionales críticas, primariamente:
   - `Account`
   - `StockEntry`
   - `PurchaseOrder`
   - `Item`

2. **Persistencia (`Erp.Infrastructure\Data\Configurations\`):** 
   Configurar la propiedad mediante Fluent API en los `IEntityTypeConfiguration` correspondientes utilizando:
   `builder.Property(x => x.RowVersion).IsRowVersion();`

3. **Migración:**
   Una vez agregadas, ejecuta el comando para crear la migración de EF Core:
   `dotnet ef migrations add AddOptimisticConcurrencyRowVersion -p Erp.Infrastructure -s Erp.AppHost`

4. **Validación:**
   Revisar que los repositorios o los handlers de MediatR manejen correctamente la excepción `DbUpdateConcurrencyException` devolviendo un `Result.Failure` con el error tipado correspondiente.

Procedé generando el reporte de la Fase 1 e implementando los cambios de la Fase 2, asegurándote de no romper las pruebas unitarias existentes.
```

---

## 2. Auditoría e Implementación de Frontend
**Agente requerido:** `@expert-react-frontend-engineer`

```markdown
Actúa como `@expert-react-frontend-engineer` para realizar una auditoría arquitectónica y posterior refactorización del proyecto frontend en `aspire-erp/src/Frontend/erp-client/`.

### FASE 1: Auditoría de Arquitectura (React 19.2 & TypeScript)
1. **Adopción de React 19.2:**
   - Evaluar si se están utilizando los patrones modernos: Server Components vs Client Components, el hook `use()` para promesas/contextos, y `<Activity>` para vistas en background.
   - Revisar el manejo de formularios y estados asíncronos (uso de `useActionState`, `useFormStatus` y `useOptimistic`).
2. **Estructura y Tipado:**
   - Verificar la estructura del proyecto (Container-Presentational, Atomic Design, o Feature-Sliced Design).
   - Auditar el nivel de rigor en TypeScript (estrictez, uso de interfaces coherentes con los DTOs del backend).
3. **Manejo de Errores y Estados:**
   - Revisar la implementación de Error Boundaries y Suspense.
   - Generar un reporte rápido indicando áreas de mejora y deuda técnica arquitectónica.

### FASE 2: Implementación de Resiliencia para el ERP
Para alinearnos con los requerimientos estrictos de los módulos de negocio (ej. `01-accounting`, `02-stock`), necesitamos interfaces altamente transaccionales y resilientes:

1. **Hooks Transaccionales:**
   - Crear o refactorizar un hook genérico de mutación (`useErpAction`) que envuelva `useActionState`, maneje automáticamente los tokens de idempotencia hacia el backend, e integre `useOptimistic` para un feedback inmediato en la UI.
2. **Integración de Tipos:**
   - Asegurar que el manejo de respuestas del backend respete el formato de `Result.Failure` (Result Pattern), extrayendo los códigos de error (ej: `ConcurrencyException`, `InsufficientStockException`) y mostrándolos de forma amigable al usuario.
3. **Vistas Protegidas:**
   - Envolver las vistas transaccionales principales (ej: Alta de Asientos Contables, Ingreso de Stock) con límites de Suspense y optimizar su renderizado para evitar bloqueos del hilo principal en tablas con gran volumen de datos.

Procedé ejecutando el análisis de la Fase 1 y luego implementando las bases arquitectónicas descritas en la Fase 2, asegurando código limpio y documentado.
```

---

## 3. Ejecución Full-Stack por Módulo (Spec Kit)
**Agentes requeridos:** `@dotnet-core-expert`, `@database-optimizer` y `@expert-react-frontend-engineer`

```markdown
Actúa conjuntamente como `@dotnet-core-expert`, `@database-optimizer` y `@expert-react-frontend-engineer`. Vamos a iniciar la implementación y alineación Full-Stack del código existente bajo la nueva metodología del Spec Kit, comenzando por el núcleo del sistema: **01-accounting**.

### Instrucciones de Ejecución:
1. **Lectura de Especificaciones:**
   Antes de tocar código, lean estrictamente en este orden los artefactos certificados del módulo:
   - `aspire-erp/.specify/modules/archive/2026-10-04-01-accounting/spec.md` (Entendimiento de negocio, UX y reglas).
   - `aspire-erp/.specify/modules/archive/2026-10-04-01-accounting/plan.md` (Decisiones de arquitectura Backend y Frontend).
   - `aspire-erp/.specify/modules/archive/2026-10-04-01-accounting/tasks.md` (Lista de tareas accionables).

2. **Auditoría de Código Existente vs Nuevo Spec:**
   - **Backend/DB:** Analicen el código en `src/Backend/` relacionado con `Account` y `GLEntry`. Verifiquen consistencia transaccional, `RowVersion`, y aserciones de doble partida.
   - **Frontend:** Analicen el código en `src/Frontend/erp-client/` asegurando el uso de `useErpAction`, Server/Client components (React 19.2), validaciones de UI, y tipado estricto contra los DTOs.

3. **Ejecución Ordenada:**
   Comiencen a implementar/refactorizar siguiendo estrictamente el orden de `tasks.md` del módulo `01-accounting`.
   - El trabajo debe coordinarse: el backend expone el endpoint y el contrato; el frontend lo consume implementando Suspense y manejo de errores nativo.
   - Por cada tarea completada, marquen su casilla en el archivo `tasks.md` actualizando su estado.
   - Deténganse al finalizar un bloque lógico mayor. Generen un reporte breve de las integraciones logradas y esperen mi confirmación para avanzar al siguiente bloque.

Procedan leyendo los 3 archivos del Spec Kit del módulo 01 y denme su reporte de estado inicial junto con la primera tarea a ejecutar.
```
