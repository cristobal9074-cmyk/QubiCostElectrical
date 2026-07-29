# QUBI COST ELÉCTRICO — FASE 11D
## Reajustes Contractuales, Garantías, Multas y Facturación Informativa

## Fuente única de alcance

Este documento define exclusivamente la Fase 11D.

No implementar funcionalidades de la Fase 11E.

---

# Reglas de ahorro de tokens

1. Leer únicamente este documento y AGENTS.md.
2. No recorrer todo el repositorio.
3. Trabajar solo sobre módulos contractuales relacionados.
4. No leer docs/archive.
5. No reproducir archivos completos en el chat.
6. Usar tests filtrados durante el desarrollo.
7. Ejecutar la suite completa solo al inicio y al final.
8. No usar subagentes.
9. No ejecutar Git.
10. No crear documentación innecesaria.

---

# Objetivo

Completar el módulo contractual profesional incorporando:

- reajustes contractuales
- fórmulas polinómicas
- índices económicos
- garantías
- boletas
- pólizas
- multas
- penalidades
- facturación informativa
- vencimientos
- renovaciones
- alertas
- auditoría
- persistencia
- integración WPF
- exportación

Todo debe integrarse con:

- Contratos
- Órdenes de Cambio
- Estados de Pago
- Procurement
- Budget Engine
- Planning
- EVM

No duplicar motores existentes.

---

# 1. Reajustes

Implementar:

- ContractAdjustment
- AdjustmentFormula
- AdjustmentIndex
- AdjustmentCalculation
- AdjustmentSnapshot

Tipos:

- IPC
- UF
- Índice personalizado
- Fórmula polinómica
- Factor fijo
- Manual justificado

Cada reajuste debe almacenar:

- contrato
- período
- fecha base
- fecha cálculo
- índices utilizados
- coeficientes
- monto base
- monto reajustado
- diferencia
- moneda
- auditoría
- hash SHA-256

No modificar el monto original del contrato.

---

# 2. Fórmulas polinómicas

Permitir definir fórmulas compuestas por:

- mano de obra
- materiales
- equipos
- combustibles
- transporte
- otros índices

Cada componente tendrá:

- ponderación
- índice base
- índice actual

El motor debe ser determinista y reutilizable.

---

# 3. Garantías

Implementar:

- ContractGuarantee
- GuaranteeType
- GuaranteeStatus

Tipos mínimos:

- Seriedad de oferta
- Fiel cumplimiento
- Correcta ejecución
- Anticipo
- Buena ejecución
- Retención
- Otra

Campos:

- número
- emisor
- aseguradora/banco
- beneficiario
- monto
- porcentaje
- vigencia
- vencimiento
- renovaciones
- estado
- documentos
- auditoría

Estados:

- Draft
- Active
- Renewed
- Released
- Expired
- Executed
- Cancelled

---

# 4. Alertas

Crear alertas para:

- garantía próxima a vencer
- póliza vencida
- reajuste pendiente
- multa pendiente
- factura pendiente
- documento incompleto

Permitir filtros por:

- prioridad
- contrato
- fecha
- responsable

---

# 5. Multas

Implementar:

- ContractPenalty
- PenaltyCalculation
- PenaltyReason

Tipos:

- atraso
- incumplimiento
- calidad
- seguridad
- contractual
- administrativa

Cada multa debe registrar:

- contrato
- causa
- fecha
- monto
- porcentaje
- estado
- observaciones
- auditoría

Estados:

- Draft
- Proposed
- UnderReview
- Approved
- Rejected
- Applied
- Closed

No descontar automáticamente de estados de pago.

Solo registrar impacto contractual.

---

# 6. Facturación informativa

Implementar:

- ContractInvoice
- InvoiceLine

Campos:

- número
- fecha
- proveedor
- cliente
- moneda
- subtotal
- impuestos
- total
- referencia al estado de pago
- referencia al contrato

No reemplaza un ERP.

Es únicamente seguimiento contractual.

---

# 7. Integridad

Aplicar:

- SHA-256
- snapshots
- auditoría
- versionado
- comparación

para:

- reajustes
- garantías
- multas
- facturas

---

# 8. Seguridad

Aplicar:

- SoD
- AuthorizationGuard
- permisos granulares
- documentos aprobados inmutables

Nadie puede aprobar un documento creado o editado por sí mismo.

---

# 9. Persistencia

Crear únicamente las tablas necesarias.

Mantener:

- migraciones
- índices
- FK
- RowVersion
- transacciones
- CancellationToken

No modificar migraciones históricas.

---

# 10. WPF

Crear:

- AdjustmentWorkspace
- GuaranteeWorkspace
- PenaltyWorkspace
- InvoiceWorkspace

Cada módulo debe incluir:

- View
- ViewModel
- filtros
- búsqueda
- navegación
- permisos
- carga cancelable
- exportación

Integrar en:

Gestión Contractual

---

# 11. Exportación

Permitir exportar:

- reajustes
- garantías
- multas
- facturas
- auditoría

Usar infraestructura existente.

---

# 12. Tests

Cubrir únicamente:

- cálculos
- fórmulas
- reajustes
- vencimientos
- garantías
- multas
- facturas
- auditoría
- persistencia
- permisos
- SoD
- navegación
- exportación

No crear pruebas redundantes.

---

# 13. Documentación

Actualizar:

- task.md
- walkthrough.md

Crear:

- contract-adjustment-specification.md
- guarantee-management-specification.md
- penalty-management-specification.md
- invoice-tracking-specification.md
- phase-11d-report.md

---

# Validación final

Ejecutar una sola vez:

dotnet restore

Build Debug

Build Release

Suite completa

Arquitectura

Resultado obligatorio:

- 0 errores
- 0 advertencias
- 0 fallos
- 0 omitidos

---

# Criterio de cierre

Declarar únicamente:

FASE 11D — REAJUSTES, GARANTÍAS, MULTAS Y FACTURACIÓN INFORMATIVA COMPLETADA

cuando:

- todos los módulos funcionen
- WPF esté integrada
- persistencia validada
- auditoría operativa
- permisos completos
- exportación funcional
- Debug limpio
- Release limpio
- suite global completamente verde

No iniciar Fase 11E.
