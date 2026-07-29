# Revisión modelo de identidad (PIN local vs identidad corporativa)

## Estado actual

Modelo implementado: cuentas locales por archivo/equipo con PIN.

### Fortalezas

- Cero dependencia de internet/servicios externos.
- Implementación simple y soporte barato en instalaciones pequeñas.
- Muy adecuado para operación en obra y entornos desconectados.

### Debilidades

- No hay SSO ni integración con directorio corporativo.
- Gestión de credenciales distribuida (cada equipo/instancia).
- Menor percepción “enterprise” en cuentas grandes.

## Recomendación comercial

## Corto plazo (v1.x)

Mantener **PIN local** como default para mercado objetivo (3–8 estimadores), reforzando:

- UX de administración de usuarios,
- política PIN clara,
- trazabilidad de cambios.

## Mediano plazo (v2)

Evaluar modo **Identidad corporativa opcional** (Azure AD/Google Workspace/OIDC) para clientes enterprise, sin romper modo local.

## Decisión formal v1.x (2026-07-02)

**Aprobado para go-to-market 1.0.x:** autenticación **PIN local** como estándar único.

| Opción | Decisión | Motivo |
|--------|----------|--------|
| PIN local endurecido | **Mantener (v1.x)** | Mercado objetivo 3–8 estimadores, obra sin red, soporte simple |
| SSO / OIDC corporativo | **Diferido a v2 (épica)** | No bloquea ventas actuales; add-on enterprise futuro |

No se abre trabajo de SSO en esta release. La trazabilidad de administración (auditoría SQLite, desbloqueo admin, política PIN configurable) cubre el gap operativo de equipos pequeños.

## Decisión propuesta (histórico)

- **Go-to-market inmediato:** PIN local endurecido (actual enfoque).
- **Roadmap premium:** módulo corporativo opcional (add-on) para cuentas con exigencia de SSO/auditoría centralizada.
