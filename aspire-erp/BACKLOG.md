# Backlog & Technical Debt

## Technical Debt
- **[Assets] Flujo de Capitalización de Activos**: Actualmente no es posible crear un Activo Fijo directamente desde el módulo de activos (por integridad contable). Falta implementar el flujo completo donde al registrar una Factura de Compra (Módulo de Compras) o mediante una Carga Inicial (Asiento de Apertura), se dispare la creación automática del Activo Fijo en estado "Borrador" o "Capitalizado".
