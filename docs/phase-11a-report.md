# Informe de Fase 11A — Contratos y Administración Documental

## Resumen del Trabajo Realizado
- **Modelo de Datos**: Se implementó el agregado `Contract` y los objetos dependientes `ContractParty`, `ContractScopeLink`, `ContractMilestone` y `ContractDocument`.
- **Integridad y Versionamiento**: Se desarrolló un sistema determinista de snapshots e integridad mediante hashing SHA-256.
- **Application Layer**: Se creó `ContractService` aplicando la segregación de funciones (SoD) y control de concurrencia.
- **SQLite**: Se añadió la migración `48` para crear las tablas correspondientes e índices.
- **WPF**: Se diseñaron las pantallas `ContractWorkspaceView`, `ContractEditorView` y `ContractVersionsView` con sus correspondientes ViewModels.
- **Pruebas**: Se agregaron más de 100 aserciones de prueba cubriendo flujos de negocio, integridad, SoD, persistencia SQLite y navegación.
