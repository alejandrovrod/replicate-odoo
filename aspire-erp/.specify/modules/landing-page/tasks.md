# Implementation Tasks: Aspire ERP Showcase Landing Page

> **Status:** APPROVED & READY FOR APPLY
> **Target Module:** `frontend/erp-client/src/features/landing`
> **Prerequisites:** React 19, Lucide React, Tailwind CSS, i18next

---

## Task Breakdown

### Phase 1: Internationalization & Localization Dictionaries
- [ ] **T1.1**: Create `public/locales/es/landing.json` with all copy in Spanish (Hero, Badges, Chameleon verticals, Modules grid, Technical engineering specs, CTA, Footer).
- [ ] **T1.2**: Create `public/locales/en/landing.json` with complete copy in English.
- [ ] **T1.3**: Register the `landing` namespace in `src/frontend/erp-client/src/i18n.ts` to ensure automatic loading.

### Phase 2: Feature Components (`src/features/landing/`)
- [ ] **T2.1**: Implement `LandingNavbar.tsx`:
  - Brand logo ("Aspire ERP") with badge (".NET 10 Enterprise").
  - Quick anchors (`#modules`, `#chameleon`, `#architecture`).
  - Language toggle (`ES` / `EN`) using `i18next`.
  - Primary CTA button "Iniciar Sesión" / "Login" leading to the login view.
- [ ] **T2.2**: Implement `HeroSection.tsx`:
  - Modern gradient typography and background grid glow effect.
  - Core value propositions: .NET 10 Aspire, SQL Server 2025, Chameleon Architecture, ERPNext parity.
  - Interactive Action buttons: "Ingresar al Demo" (sets demo credentials and logs in or opens login) and "Ver Arquitectura".
  - Floating metric cards: High concurrency, Zero discrepancy double entry, Instant Chameleon skinning.
- [ ] **T2.3**: Implement `ChameleonShowcase.tsx`:
  - Live interactive tabs to simulate industry skins:
    - 🏥 Healthcare (Pacientes, Admisiones, Citas)
    - 🚗 Automotive (Vehículos, Órdenes de Servicio, Propietarios)
    - 🏗️ Construction (Obras, Subcontratistas, Maquinaria)
    - 📦 Distribution (Puntos de Venta, Surtido, Landed Cost)
  - Visual preview card illustrating how vocabulary and flows dynamically adjust.
- [ ] **T2.4**: Implement `ModulesBentoGrid.tsx`:
  - Bento-grid style layout highlighting:
    - 📊 General Ledger & Treasury (DoubleEntryGuard, hierarchical charts)
    - 🧾 Sales & Billing (ERPNext parity: Update Stock, multi-currency)
    - 📦 Warehouse & Inventory (FIFO perpetual valuation, Serial/Batch tracking, Landed Cost)
    - 🏭 Manufacturing & BOM (Multilevel bills of materials, Workstations)
    - 🏢 Fixed Assets (Linear depreciation, asset custodian)
- [ ] **T2.5**: Implement `TechStackSection.tsx`:
  - Deep-dive cards into engineering rigor:
    - Clean Architecture (.NET 10, MediatR CQRS, EF Core)
    - High-throughput persistence (SQL Server 2025, system versioning)
    - Modern Frontend (React 19, TypeScript, Radix UI, Zustand)
- [ ] **T2.6**: Implement `LandingFooter.tsx`:
  - Clean footer with copyright, links, and quick login trigger.
- [ ] **T2.7**: Assemble `LandingPage.tsx` uniting all subcomponents with smooth scrolling and responsive layout.

### Phase 3: Integration & Routing
- [ ] **T3.1**: Update `App.tsx` and `useNavigationStore.ts`:
  - When unauthenticated and route is `#landing` or root (`#/`), render `<LandingPage onLogin={() => setRoute('login')} />`.
  - Provide easy transition to `<LoginScreen />` via navbar button or hero CTA.
  - Provide a "Volver a la Landing" link in `LoginScreen` for visitors.

### Phase 4: Validation & Quality Gate
- [ ] **T4.1**: Verify responsive design on mobile and desktop viewports.
- [ ] **T4.2**: Verify language switching between `es` and `en` updates all landing page strings without reload.
- [ ] **T4.3**: Verify TypeScript type-checking (`tsc --noEmit`) and Vite hot-reloading with no console warnings.
