# Spec — Aspire ERP Showcase Landing Page & Product Portal

> **STATUS: DRAFT — READY FOR IMPLEMENTATION**
> **Standards: Clean Architecture, Modern UI/UX, Component Isolation, React 19, i18n, SEO-Ready**

---

## 0. Propósito y Contexto de Negocio

Actualmente, **Aspire ERP** inicia directamente en la pantalla de autenticación (`LoginScreen`) o en el panel de control operativo (`DashboardOverview`). 

Se requiere una **Landing Page institucional interactiva y moderna** accesible públicamente antes del login (o como portal informativo del producto) que sirva para:
1. **Destacar los pilares técnicos y de ingeniería del sistema:** Arquitectura Limpia (.NET 10 + Aspire), base de datos de alta concurrencia SQL Server 2025, inmutabilidad contable (DoubleEntryGuard) y valuación perpetua de stock (FIFO).
2. **Presentar la Arquitectura Camaleónica en acción:** Demostrador visual interactivo donde el visitante pueda alternar entre industrias (*Healthcare, Automotive, Construction, Distribution*) y ver cómo la interfaz y el vocabulario se transforman en tiempo real.
3. **Exhibir los módulos de nivel ERPNext:** Contabilidad y Finanzas, Facturación con entrega directa, Costos de Adquisición (Landed Cost), Trazabilidad por Lote/Serie, Gestión de Activos Fijos y Recursos Humanos.
4. **Proporcionar un llamado a la acción claro (CTA):** Acceso directo a la plataforma ("Ingresar al Sistema" / "Probar Demo") con credenciales pre-cargadas para evaluación rápida.

---

## 1. Gap Matrix

| # | Dimensión | Estado Actual | Solución a Implementar | Decisión Arquitectónica |
|---|-----------|---------------|------------------------|--------------------------|
| D1 | Puerta de Entrada Pública | Solo existe `LoginScreen` con campos de usuario y contraseña | Landing Page visual moderna con barra de navegación, hero section, módulos y footer | Nueva ruta pública `landing` o render condicional cuando el usuario no está autenticado |
| D2 | Demostración Camaleónica | Los presets solo se prueban dentro de configuración contable | Componente interactivo "Camaleón Explorer" en la landing para previsualizar verticales sin login | Componente desacoplado que usa los mockups de vocabulario del catálogo de presets |
| D3 | Navegación e Identidad | No hay menú público ni resumen de capacidades | Barra de navegación superior con selector de idioma (`es` / `en`), links a módulos y botón de "Iniciar Sesión" | Componente `LandingNavbar` integrado con `i18next` y `aspire-erp-lang` |
| D4 | Rendimiento y Animaciones | Sin animaciones promocionales | Micro-interacciones con Tailwind CSS y componentes visuales reactivos sin dependencias pesadas | Vanilla Tailwind + Radix UI (manteniendo el bundle liviano) |

---

## 2. Secciones de la Landing Page

### 1. Hero Section (Primer Impacto)
- **Título:** "El ERP Empresarial de Nueva Generación diseñado para escalar".
- **Subtítulo:** "Potenciado por .NET 10 Aspire, SQL Server 2025 y Arquitectura Camaleónica con paridad total ERPNext".
- **Badges de Confianza:** "Paridad Contable Doble Partida", "Valuación FIFO de Inventario", "Multi-Tenant Aislado", "100% Código Abierto".
- **Acciones:** Botón principal *"Explorar Plataforma"* (conduce al login o abre la demo) y botón secundario *"Ver Arquitectura Técnica"*.

### 2. Demostrador Interactivo: "Arquitectura Camaleónica"
- Selector con pestañas:
  - 🏥 **Clínica / Healthcare:** Muestra cómo el ERP adopta automáticamente términos como *Pacientes*, *Órdenes de Admisión*, flujos médicos y retenciones de salud.
  - 🚗 **Taller Mecánico / Automotive:** Muestra *Propietarios*, *Órdenes de Servicio*, *Vehículos con VIN* y *Repuestos*.
  - 🏗️ **Constructora / Construction:** Muestra *Subcontratistas*, *Órdenes de Obra*, *Maquinaria* y *Frentes de Trabajo*.
  - 📦 **Distribuidora / Distribution:** Muestra *Puntos de Venta*, *Pedidos de Surtido*, *Productos con Código de Barras* y *Landed Cost*.

### 3. Matriz de Módulos Enterprise (Grid de Bondades)
- **Contabilidad & Tesorería:** Plan de cuentas jerárquico multinivel, libro mayor inmutable, conciliación bancaria y revaluaciones FX automáticas.
- **Ventas & Facturación (ERPNext Parity):** Facturas con entrega directa de stock (`Update Stock`), notas de crédito, cruce de anticipos y puntos de venta (POS).
- **Inventario & Kardex:** Costeo FIFO estricto, gestión de lotes y números de serie, almacenes múltiples y Landed Cost Vouchers para prorrateo de fletes.
- **Activos Fijos:** Depreciación contable en línea recta, capitalización de gastos y custodia de activos.
- **Manufactura & LDM:** Listas de materiales (BOM) multinivel, estaciones de trabajo y órdenes de producción.

### 4. Stack Técnico & Especificaciones de Ingeniería
- **Backend:** C# / .NET 10, ASP.NET Core Minimal APIs & Controllers, MediatR (CQRS), Entity Framework Core con migración automática.
- **Base de Datos:** Microsoft SQL Server 2025 con tablas versionadas por el sistema (`SYSTEM_VERSIONING = ON`), índices compuestos no agrupados y concurrencia optimista (`RowVersion`).
- **Frontend:** React 19, TypeScript estricto, Tailwind CSS, Vite, Zustand para gestión de estado liviana, i18next internacionalizado (`es`/`en`).

### 5. Call to Action & Footer
- Acceso directo con un solo click a la versión de prueba / inicio de sesión.
- Enlaces a documentación, código fuente y aviso legal.

---

## 3. Arquitectura Frontend & Ruteo

```
[Usuario no Autenticado]
        │
        ├─── URL: / ó #landing  ────────► <LandingPage />
        │                                    ├── <LandingNavbar />
        │                                    ├── <HeroSection />
        │                                    ├── <ChameleonShowcase />
        │                                    ├── <ModulesBentoGrid />
        │                                    ├── <ArchitectureStackSection />
        │                                    └── <LandingFooter />
        │
        └─── Botón "Iniciar Sesión" ─────► <LoginScreen /> (Modal o Vista)
```

---

## 4. Textos y Diccionarios de Traducción (`locales/{es,en}/landing.json`)

Se creará un nuevo namespace `landing` en `public/locales/es/landing.json` y `public/locales/en/landing.json` para soportar internacionalización nativa y evitar textos fijos en el código.
