# Casos de prueba

## Objetivo

Esta lista define pruebas minimas para evitar romper calculos, persistencia, UI critica y exportacion.

## Pruebas existentes

La solucion contiene pruebas en:

- `QubiCostElectrical.Tests/Application/BudgetCalculationServiceTests.cs`
- `QubiCostElectrical.Tests/Application/ValidationServiceTests.cs`
- `QubiCostElectrical.Tests/Domain/ApuEngineTests.cs`
- `QubiCostElectrical.Tests/Domain/BudgetEngineTests.cs`

Nota: estados de pago (`EstadoPagoService`) están fuera de alcance de 1.0.0 — ver `docs/BC3_SCOPE.md` y `TODOS.md` ("Posicionamiento: presupuesto y oferta eléctrica. No obra/estados de pago").

Comando:

```powershell
dotnet test QubiCostElectrical.Wpf.sln --no-build
```

## Pruebas manuales obligatorias despues de cambios UI

1. Abrir la aplicacion.
2. Crear proyecto.
3. Editar datos del proyecto.
4. Guardar.
5. Cerrar y abrir proyecto.
6. Entrar a Itemizado.
7. Entrar a Cubicacion.
8. Agregar material.
9. Cambiar cantidad.
10. Ver totales.
11. Entrar a APU.
12. Cambiar de partida sin parpadeo visible.
13. Entrar a C.SEC y crear tablero/circuito.
14. Enviar C.SEC a presupuesto.
15. Ejecutar revision final.
16. Exportar Excel.

## Casos de calculo

- Cubicacion con material y mano de obra.
- Cantidad cero genera advertencia.
- Cantidad negativa se permite como ajuste.
- Material sin precio genera advertencia.
- Precio negativo genera error.
- GG y utilidad no pueden ser negativos.
- CLP/USD requieren UF valida.
- USD requiere tipo de cambio valido.

## Casos APU

- APU sin recursos genera error.
- Recurso con rendimiento cero genera error.
- Recurso sin unidad genera advertencia.
- Merma afecta materiales.
- Herramientas se calculan sobre MO/HH si no hay recurso explicito.
- Transporte se calcula si no hay recurso TR explicito.
- Total partida = precio unitario * cantidad partida.

## Casos SQLite

- Guardar snapshot completo.
- Guardado incremental de `cubicacion`.
- Guardado incremental de `apu`.
- Cargar proyecto guardado.
- Migrar base antigua.
- Validar `PRAGMA quick_check`.
- No perder JSON de propiedades nuevas.

## Casos exportacion

- Excel contiene hojas esperadas.
- Totales coinciden con `BudgetEngine`.
- Moneda coincide con configuracion del proyecto.
- Oferta cliente no muestra datos internos.
- Revision final aparece en hoja interna.

## Casos rendimiento

- Catalogo de materiales grande busca sin congelar UI.
- Cubicacion con muchas filas no pierde seleccion.
- Cambio de partida no debe mostrar pantalla en blanco.
- C.SEC con muchos circuitos no debe recalcular visualmente todo si no es necesario.

