using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class ProyectoViewModelService : IProyectoViewModelService
{
    private readonly IBudgetCalculationService budgetCalculationService;
    private readonly ICubicacionViewModelService cubicacionViewModelService;

    public ProyectoViewModelService(IBudgetCalculationService budgetCalculationService, ICubicacionViewModelService cubicacionViewModelService)
    {
        this.budgetCalculationService = budgetCalculationService;
        this.cubicacionViewModelService = cubicacionViewModelService;
    }

    public ProyectoViewModel Build(
        string nombre,
        string cliente,
        string ubicacion,
        string usuarioNombre,
        string usuarioRol,
        string moneda,
        string estadoPresupuesto,
        DateTime fechaCreacion,
        DateTime fechaModificacion,
        decimal valorUfClp,
        decimal tipoCambioUsdClp,
        decimal gastosGeneralesPct,
        decimal utilidadPct,
        decimal metrosCuadrados,
        IEnumerable<ItemizadoRow> itemizado,
        IEnumerable<MaterialRow> materiales,
        IEnumerable<CubicacionRow> cubicacion)
    {
        var itemList = itemizado?.ToList() ?? new List<ItemizadoRow>();
        var materialList = materiales?.ToList() ?? new List<MaterialRow>();
        var cubicacionVm = cubicacionViewModelService.Build(cubicacion, "", gastosGeneralesPct, utilidadPct, metrosCuadrados);
        var areas = budgetCalculationService.GetAreaTotals(cubicacion ?? Enumerable.Empty<CubicacionRow>())
            .Select(x => (x.Area, TotalUf: x.Total))
            .ToList();

        return new ProyectoViewModel(
            string.IsNullOrWhiteSpace(nombre) ? "Proyecto sin nombre" : nombre.Trim(),
            string.IsNullOrWhiteSpace(cliente) ? "Cliente sin nombre" : cliente.Trim(),
            string.IsNullOrWhiteSpace(ubicacion) ? "Sin ubicación" : ubicacion.Trim(),
            string.IsNullOrWhiteSpace(usuarioNombre) ? Environment.UserName : usuarioNombre.Trim(),
            string.IsNullOrWhiteSpace(usuarioRol) ? "Usuario" : usuarioRol.Trim(),
            string.IsNullOrWhiteSpace(moneda) ? "CLP" : moneda.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(estadoPresupuesto) ? "Borrador" : estadoPresupuesto.Trim(),
            fechaCreacion,
            fechaModificacion,
            valorUfClp,
            tipoCambioUsdClp,
            gastosGeneralesPct,
            utilidadPct,
            metrosCuadrados,
            itemList.Count(x => !x.EsTitulo),
            itemList.Count(x => x.EsTitulo),
            materialList.Count,
            cubicacionVm.LineasValorizadas,
            cubicacionVm.CantidadTotal,
            cubicacionVm.Summary,
            areas);
    }
}
