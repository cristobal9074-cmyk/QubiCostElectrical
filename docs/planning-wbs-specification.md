# Planning WBS Specification

This document details the hierarchy, sorting, validations, and linkages of the Work Breakdown Structure in planning.

## WBS Nodes
A WbsNode represents a node in the planning breakdown structure.
- **WbsNodeType**:
  - `Proyecto` (Root)
  - `Fase`
  - `Capitulo`
  - `Subcapitulo`
  - `PaqueteTrabajo` (Holds activities)
  - `Actividad`
  - `Hito`
  - `Resumen`

## Validation Rules
1. **Cycle Detection**: Parent changes are validated to prevent any circular dependencies.
2. **Move Operations**: Mapped nodes can be reordered using safe sorting limits (`SortOrder`).
3. **Budget Linkage**: Activities inside WBS Nodes link back to active budget items (Partidas) by assigning a percentage allocation or unit-based quantities.
4. **Logical Delete**: Soft-deletes nodes and verifies no active child nodes or activities remain.
