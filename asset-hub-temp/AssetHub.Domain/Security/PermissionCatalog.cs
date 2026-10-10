using System.Collections.Generic;

namespace AssetHub.Domain.Security;

/// <summary>
/// Catalogo global de permisos {module}:{entity}:{action} simplificado a
/// {modulo}:{accion}. PlanModule vacio = permiso base (todos los planes);
/// si tiene valor, el permiso requiere ese modulo habilitado en el plan del
/// tenant (regla R-ROLE-4).
/// </summary>
public static class PermissionCatalog
{
    public sealed record PermissionDefinition(string Code, string Module, string PlanModule, string Description);

    public static readonly IReadOnlyList<PermissionDefinition> All = new[]
    {
        // M1 — Landing
        new PermissionDefinition("landing:view", "Landing", "", "Ver la página pública de landing"),
        new PermissionDefinition("landing:signup", "Landing", "", "Crear nuevas cuentas de tenant"),

        // M2 — Tenancy & Onboarding
        new PermissionDefinition("tenant:read", "Organización", "", "Ver información del tenant"),
        new PermissionDefinition("tenant:write", "Organización", "", "Actualizar configuración del tenant"),
        new PermissionDefinition("user:invite", "Organización", "", "Invitar usuarios al tenant"),
        new PermissionDefinition("user:manage", "Organización", "", "Gestionar usuarios (activar, desactivar, cambiar rol)"),

        // M3 — Seguridad
        new PermissionDefinition("roles:read", "Seguridad", "", "Ver roles y permisos"),
        new PermissionDefinition("roles:manage", "Seguridad", "", "Crear/actualizar/eliminar roles y asignar permisos"),
        new PermissionDefinition("users:read", "Seguridad", "", "Ver listado y perfiles de usuarios"),
        new PermissionDefinition("users:manage", "Seguridad", "", "Crear/actualizar/desactivar usuarios"),
        new PermissionDefinition("mfa:configure", "Seguridad", "", "Habilitar/deshabilitar MFA para sí mismo"),
        new PermissionDefinition("mfa:force", "Seguridad", "", "Forzar política MFA para el tenant"),
        new PermissionDefinition("audit:read", "Seguridad", "", "Ver registros de auditoría"),

        // M4 — Planes y suscripciones
        new PermissionDefinition("billing:read", "Facturación", "", "Ver detalles del plan"),
        new PermissionDefinition("billing:subscribe", "Facturación", "", "Cambiar suscripción/plan"),
        new PermissionDefinition("billing:manage", "Facturación", "", "Gestionar facturación (facturas, métodos de pago)"),

        // M5 — Catálogos dinámicos
        new PermissionDefinition("catalogs:read", "Catálogos", "", "Ver catálogos"),
        new PermissionDefinition("catalogs:create", "Catálogos", "", "Crear catálogos"),
        new PermissionDefinition("catalogs:update", "Catálogos", "", "Actualizar catálogos"),
        new PermissionDefinition("catalogs:delete", "Catálogos", "", "Eliminar catálogos"),
        new PermissionDefinition("catalog-items:read", "Catálogos", "", "Ver items de catálogo"),
        new PermissionDefinition("catalog-items:create", "Catálogos", "", "Crear items de catálogo"),
        new PermissionDefinition("catalog-items:update", "Catálogos", "", "Actualizar items de catálogo"),
        new PermissionDefinition("catalog-items:delete", "Catálogos", "", "Eliminar items de catálogo"),

        // M6 — Tipos de entidad
        new PermissionDefinition("entity-types:read", "Tipos de Entidad", "", "Ver tipos de entidad"),
        new PermissionDefinition("entity-types:create", "Tipos de Entidad", "", "Crear tipos de entidad"),
        new PermissionDefinition("entity-types:update", "Tipos de Entidad", "", "Actualizar tipos de entidad"),
        new PermissionDefinition("entity-types:delete", "Tipos de Entidad", "", "Eliminar tipos de entidad"),

        // M7 — Templates de activos
        new PermissionDefinition("asset-templates:read", "Plantillas de Activos", "assets", "Ver plantillas de activos"),
        new PermissionDefinition("asset-templates:create", "Plantillas de Activos", "assets", "Crear plantillas de activos"),
        new PermissionDefinition("asset-templates:update", "Plantillas de Activos", "assets", "Actualizar plantillas de activos"),
        new PermissionDefinition("asset-templates:delete", "Plantillas de Activos", "assets", "Eliminar plantillas de activos"),
        new PermissionDefinition("asset-templates:clone", "Plantillas de Activos", "assets", "Clonar/versionar plantillas de activos"),

        // M8 — Activos
        new PermissionDefinition("assets:read", "Activos", "assets", "Ver activos"),
        new PermissionDefinition("assets:create", "Activos", "assets", "Crear activos"),
        new PermissionDefinition("assets:update", "Activos", "assets", "Actualizar activos"),
        new PermissionDefinition("assets:delete", "Activos", "assets", "Eliminar activos (soft delete)"),
        new PermissionDefinition("assets:move", "Activos", "assets", "Mover activos en la jerarquía"),
        new PermissionDefinition("assets:change-state", "Activos", "assets", "Cambiar estado de activos vía lifecycle"),
        new PermissionDefinition("assets:attachments", "Activos", "assets", "Subir/descargar adjuntos de activos"),
        new PermissionDefinition("assets:analytics", "Activos", "assets", "Ver analíticas/reportes de activos"),

        // M9 — Características EAV
        new PermissionDefinition("assets-properties:read", "Activos", "assets", "Ver propiedades de activos"),
        new PermissionDefinition("assets-properties:write", "Activos", "assets", "Modificar valores de propiedades de activos"),

        // M10 — Geolocalización
        new PermissionDefinition("geo:read", "Geolocalización", "assets", "Ver datos geográficos"),
        new PermissionDefinition("geo:write", "Geolocalización", "assets", "Crear/actualizar datos geográficos"),

        // M11 — Incidencias y prevención
        new PermissionDefinition("incidents:read", "Incidencias", "maintenance", "Ver incidencias"),
        new PermissionDefinition("incidents:create", "Incidencias", "maintenance", "Reportar/crear incidencias"),
        new PermissionDefinition("incidents:update", "Incidencias", "maintenance", "Actualizar incidencias"),
        new PermissionDefinition("incidents:delete", "Incidencias", "maintenance", "Eliminar incidencias (soft delete)"),
        new PermissionDefinition("incidents:assign", "Incidencias", "maintenance", "Asignar incidencias a empleados"),
        new PermissionDefinition("incidents:triage", "Incidencias", "maintenance", "Definir prioridad en triage"),
        new PermissionDefinition("incidents:change-state", "Incidencias", "maintenance", "Cambiar estado de incidencias"),
        new PermissionDefinition("incidents:close", "Incidencias", "maintenance", "Cerrar/resolver incidencias"),
        new PermissionDefinition("incidents:timeline", "Incidencias", "maintenance", "Ver línea de tiempo de incidencias"),
        new PermissionDefinition("incidents:export", "Incidencias", "maintenance", "Exportar reportes de incidencias"),
        new PermissionDefinition("preventive-plans:read", "Planes Preventivos", "maintenance", "Ver planes preventivos"),
        new PermissionDefinition("preventive-plans:create", "Planes Preventivos", "maintenance", "Crear planes preventivos"),
        new PermissionDefinition("preventive-plans:update", "Planes Preventivos", "maintenance", "Actualizar planes preventivos"),
        new PermissionDefinition("preventive-plans:delete", "Planes Preventivos", "maintenance", "Eliminar planes preventivos"),
        new PermissionDefinition("preventive-plans:execute", "Planes Preventivos", "maintenance", "Ejecutar/disparar planes preventivos"),

        // M12 — Mantenimiento
        new PermissionDefinition("maintenance:read", "Mantenimiento", "maintenance", "Ver órdenes de mantenimiento"),
        new PermissionDefinition("maintenance:create", "Mantenimiento", "maintenance", "Crear órdenes de mantenimiento"),
        new PermissionDefinition("maintenance:update", "Mantenimiento", "maintenance", "Actualizar órdenes de mantenimiento"),
        new PermissionDefinition("maintenance:delete", "Mantenimiento", "maintenance", "Eliminar órdenes de mantenimiento"),
        new PermissionDefinition("maintenance:approve", "Mantenimiento", "maintenance", "Aprobar órdenes"),
        new PermissionDefinition("maintenance:schedule", "Mantenimiento", "maintenance", "Programar órdenes"),
        new PermissionDefinition("maintenance:start", "Mantenimiento", "maintenance", "Iniciar órdenes"),
        new PermissionDefinition("maintenance:complete", "Mantenimiento", "maintenance", "Completar órdenes"),
        new PermissionDefinition("maintenance:verify", "Mantenimiento", "maintenance", "Verificar órdenes completadas"),
        new PermissionDefinition("maintenance:reject", "Mantenimiento", "maintenance", "Rechazar órdenes"),
        new PermissionDefinition("maintenance:cancel", "Mantenimiento", "maintenance", "Cancelar órdenes"),
        new PermissionDefinition("maintenance:costs", "Mantenimiento", "maintenance", "Actualizar costos"),
        new PermissionDefinition("maintenance:assign", "Mantenimiento", "maintenance", "Asignar empleados a órdenes"),
        new PermissionDefinition("maintenance-parts:read", "Mantenimiento", "maintenance", "Ver repuestos"),
        new PermissionDefinition("maintenance-parts:manage", "Mantenimiento", "maintenance", "Agregar/quitar repuestos"),
        new PermissionDefinition("maintenance:export", "Mantenimiento", "maintenance", "Exportar órdenes"),

        // M13 — Personal y equipos
        new PermissionDefinition("employees:read", "Personal", "", "Ver empleados"),
        new PermissionDefinition("employees:create", "Personal", "", "Crear empleados"),
        new PermissionDefinition("employees:update", "Personal", "", "Actualizar empleados"),
        new PermissionDefinition("employees:delete", "Personal", "", "Eliminar/desactivar empleados"),
        new PermissionDefinition("employees:assign-role", "Personal", "", "Asignar/cambiar roles de empleados"),
        new PermissionDefinition("teams:read", "Personal", "", "Ver equipos"),
        new PermissionDefinition("teams:create", "Personal", "", "Crear equipos"),
        new PermissionDefinition("teams:update", "Personal", "", "Actualizar equipos"),
        new PermissionDefinition("teams:delete", "Personal", "", "Eliminar equipos"),

        // M14 — Tareas
        new PermissionDefinition("tasks:read", "Tareas", "maintenance", "Ver tareas"),
        new PermissionDefinition("tasks:create", "Tareas", "maintenance", "Crear tareas"),
        new PermissionDefinition("tasks:update", "Tareas", "maintenance", "Actualizar tareas"),
        new PermissionDefinition("tasks:delete", "Tareas", "maintenance", "Eliminar tareas"),
        new PermissionDefinition("tasks:assign", "Tareas", "maintenance", "Asignar tareas a empleados"),
        new PermissionDefinition("tasks:start", "Tareas", "maintenance", "Iniciar tareas"),
        new PermissionDefinition("tasks:complete", "Tareas", "maintenance", "Completar tareas"),
        new PermissionDefinition("tasks:cancel", "Tareas", "maintenance", "Cancelar tareas"),
        new PermissionDefinition("tasks:comment", "Tareas", "maintenance", "Agregar comentarios"),
        new PermissionDefinition("tasks:evidence", "Tareas", "maintenance", "Subir evidencias"),
        new PermissionDefinition("tasks:recurrence", "Tareas", "maintenance", "Configurar recurrencia"),

        // M15 — Seguimiento de tareas
        new PermissionDefinition("tasks-board:read", "Tareas", "maintenance", "Ver tableros de tareas (Kanban)"),
        new PermissionDefinition("tasks-board:configure", "Tareas", "maintenance", "Configurar vistas/filtros de tareas"),
        new PermissionDefinition("task-status:read", "Tareas", "maintenance", "Ver historial de estados de tareas"),
        new PermissionDefinition("task-status:update", "Tareas", "maintenance", "Actualizar progresión de estados de tareas"),

        // M16 — Análisis de vida del activo
        new PermissionDefinition("analytics:read", "Análisis", "assets", "Ver dashboards y analíticas"),
        new PermissionDefinition("analytics:export", "Análisis", "assets", "Exportar reportes"),
        new PermissionDefinition("predictions:read", "Análisis", "assets", "Ver predicciones de salud"),

        // M20 — Inventario
        new PermissionDefinition("inventory:read", "Inventario", "maintenance", "Ver configuración/stock de inventario"),
        new PermissionDefinition("inventory:configure", "Inventario", "maintenance", "Configurar inventario"),
        new PermissionDefinition("warehouses:read", "Inventario", "maintenance", "Ver almacenes"),
        new PermissionDefinition("warehouses:create", "Inventario", "maintenance", "Crear almacenes"),
        new PermissionDefinition("warehouses:update", "Inventario", "maintenance", "Actualizar almacenes"),
        new PermissionDefinition("warehouses:delete", "Inventario", "maintenance", "Eliminar almacenes"),
        new PermissionDefinition("stock:read", "Inventario", "maintenance", "Ver saldos de stock"),
        new PermissionDefinition("stock:adjust", "Inventario", "maintenance", "Crear ajustes de stock"),
        new PermissionDefinition("transactions:read", "Inventario", "maintenance", "Ver transacciones de inventario"),
        new PermissionDefinition("receipts:create", "Inventario", "maintenance", "Registrar recepciones"),

        // Plantillas de comunicación (extensión M11/M12 del sistema real)
        new PermissionDefinition("communication-templates:read", "Comunicación", "maintenance", "Ver plantillas de comunicación"),
        new PermissionDefinition("communication-templates:manage", "Comunicación", "maintenance", "Crear/actualizar/eliminar plantillas de comunicación"),

        // M21 — Finanzas, Depreciación y Bajas
        new PermissionDefinition("assets.finance:read", "Finanzas Activos", "assets", "Ver perfil financiero, cronogramas y ajustes"),
        new PermissionDefinition("assets.finance:write", "Finanzas Activos", "assets", "Crear/actualizar perfil financiero, generar schedule"),
        new PermissionDefinition("assets.depreciation:post", "Finanzas Activos", "assets", "Devengar cuotas de depreciación"),
        new PermissionDefinition("assets.value-adjustment:write", "Finanzas Activos", "assets", "Registrar revaluaciones y deterioros"),
        new PermissionDefinition("assets.disposal:read", "Finanzas Activos", "assets", "Ver bajas/desincorporaciones"),
        new PermissionDefinition("assets.disposal:write", "Finanzas Activos", "assets", "Procesar bajas definitivas de activos"),
        new PermissionDefinition("assets.custody:read", "Finanzas Activos", "assets", "Ver historial de custodias"),
        new PermissionDefinition("assets.custody:write", "Finanzas Activos", "assets", "Registrar transferencias/asignaciones de custodia"),
        new PermissionDefinition("assets.capitalization:propose", "Finanzas Activos", "assets", "Capitalizar costos de mantenimiento (CAPEX)"),
    };

    public static bool TryGet(string code, out PermissionDefinition definition)
    {
        foreach (var p in All)
        {
            if (p.Code == code)
            {
                definition = p;
                return true;
            }
        }
        definition = null!;
        return false;
    }
}
