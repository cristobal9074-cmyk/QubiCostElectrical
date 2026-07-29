# APU

## Objetivo

El APU calcula el precio unitario profesional de una partida a partir de recursos, rendimientos, mano de obra, equipos, herramientas, mermas, transporte e imprevistos.

Archivos principales:

- `QubiCostElectrical.Domain/ApuEngine/ApuEngine.cs`
- `QubiCostElectrical.Application/Services/ApuService.cs`
- `QubiCostElectrical.Application/ViewModels/ApuViewModel.cs`
- `QubiCostElectrical.Wpf/Features/Apu/ApuView.xaml` + `ApuWorkspaceVm.cs`

## Modelo

`ApuRow` representa un APU por partida:

- `ItemId`.
- `Descripcion`.
- `Unidad`.
- `CantidadPartida`.
- `Rendimiento`.
- `CuadrillaBase`.
- `HerramientasPct`.
- `MermaPct`.
- `TransportePct`.
- `ImprevistosAlzaPct`.
- `Recursos`.

`ApuRecursoRow` representa cada recurso:

- Tipo: `MAT`, `MO`, `EQ`, `SC`, `HH`, `HERR`, `TR`.
- Codigo o descripcion.
- Unidad.
- Rendimiento.
- Precio UF.
- HH, cuadrilla, fuente, proveedor, fecha y estado.

## Calculos

- `CostoMaterialUf`: suma recursos MAT.
- `CostoMoUf`: suma recursos MO.
- `CostoEquipoUf`: suma recursos EQ.
- `HerramientasCalculadoUf`: porcentaje sobre MO/HH si no existe recurso HERR explicito.
- `MermaUf`: porcentaje sobre materiales.
- `TransporteUf`: porcentaje sobre materiales/equipos si no existe recurso TR explicito.
- `ImprevistosUf`: porcentaje sobre costo directo.
- `PrecioUnitUf`: costo directo mas imprevistos.
- `TotalPartidaUf`: precio unitario por cantidad de partida.

## Reglas de UI

- El APU debe mostrar costos unitarios y total por partida.
- No debe mezclar detalles internos con resumen para cliente.
- Si falta precio o rendimiento, debe quedar visible como alerta.
- Cambiar de partida no debe generar parpadeo ni recarga perceptible.

## Validaciones

La validacion actual revisa:

- APU sin partida.
- Cantidad de partida menor o igual a cero.
- APU sin recursos.
- Materiales o mano de obra en cero.
- Precio unitario negativo.
- Recurso sin codigo, descripcion, unidad, rendimiento o precio valido.

## Pruebas recomendadas

- Crear APU desde partida.
- Migrar desde cubicacion.
- Calcular precio unitario con merma, herramientas, transporte e imprevistos.
- Validar recurso sin precio.
- Validar recurso con rendimiento cero.

