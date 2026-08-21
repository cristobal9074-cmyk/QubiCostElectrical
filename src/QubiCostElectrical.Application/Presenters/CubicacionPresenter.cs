using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record CubicacionPartidaTotals(
    decimal Materiales,
    decimal ManoObra,
    decimal CostoDirecto,
    decimal PrecioUnitario,
    decimal TotalPartida);

public sealed record CubicacionRowValidation(
    string? CodigoError,
    string? CantidadError,
    IReadOnlyList<string> Errors,
    bool HasError,
    string ErrorMessage,
    bool PrecioVencido = false);

public sealed class CubicacionPresenterServices
{
    public IBudgetCalculationService BudgetCalculation { get; }
    public CubicacionCompositionService Composition { get; }

    public CubicacionPresenterServices(
        IBudgetCalculationService budgetCalculation,
        CubicacionCompositionService composition)
    {
        BudgetCalculation = budgetCalculation ?? throw new ArgumentNullException(nameof(budgetCalculation));
        Composition = composition ?? throw new ArgumentNullException(nameof(composition));
    }
}

public sealed class CubicacionPresenter
{
    private readonly CubicacionPresenterServices _services;
    private readonly BudgetProjectSession _session;

    public CubicacionPresenter(CubicacionPresenterServices services, BudgetProjectSession session)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public CubicacionPartidaTotals CalculatePartidaTotals(string partidaId, IReadOnlyList<CubicacionRow> rows)
    {
        var validRows = rows.Where(r => !string.IsNullOrWhiteSpace(r.Codigo) || !string.IsNullOrWhiteSpace(r.Descripcion) || r.Cantidad != 0m || r.MatClp != 0m || r.MoClp != 0m || r.MatUf != 0m || r.MoUf != 0m).ToList();
        if (validRows.Count == 0)
            return new CubicacionPartidaTotals(0, 0, 0, 0, 0);

        var cost = _services.BudgetCalculation.CalculateBudget(
            validRows,
            BudgetSettings.FromValues(_session.GastosGeneralesPct, _session.UtilidadPct, _session.MetrosCuadrados));

        var item = _session.Itemizado.FirstOrDefault(x =>
            string.Equals(x.Id, partidaId, StringComparison.OrdinalIgnoreCase));
        decimal qty = item?.Cantidad ?? 1m;
        decimal precioUnitario = qty != 0 ? cost.CostoDirectoUf / qty : 0m;

        return new CubicacionPartidaTotals(
            cost.MaterialesUf,
            cost.ManoObraUf,
            cost.CostoDirectoUf,
            precioUnitario,
            cost.CostoDirectoUf);
    }

    // Un catálogo corporativo real crece con los años (miles de materiales). ValidateRow se
    // llama por CADA fila de la partida después de CADA edición (tipear un código, aplicar
    // material, pegar, mover fila...) — con un .Any()/.FirstOrDefault() lineal por fila, una
    // partida de 300 filas contra un catálogo de 10.000 materiales medía ~180ms POR EDICIÓN
    // (medido con QubiCostElectrical.Tests.Application.CubicacionValidationPerformanceTests),
    // suficiente para sentirse "pesado" tecleando. BuildMaterialesIndex() se arma UNA vez por
    // pasada de validación (no por fila) y baja el costo total a O(n+m).
    public IReadOnlyDictionary<string, MaterialRow> BuildMaterialesIndex()
    {
        var index = new Dictionary<string, MaterialRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _session.Materiales)
        {
            // TryAdd conserva el primer material con ese código, igual que el .FirstOrDefault()
            // que reemplaza, para no cambiar cuál material "gana" si el catálogo tiene códigos
            // duplicados (dato sucio, pero no es este método el que debe decidir cuál es correcto).
            if (!string.IsNullOrWhiteSpace(m.Codigo))
                index.TryAdd(m.Codigo, m);
        }
        return index;
    }

    public CubicacionRowValidation ValidateRow(CubicacionRow row) => ValidateRow(row, BuildMaterialesIndex());

    public CubicacionRowValidation ValidateRow(CubicacionRow row, IReadOnlyDictionary<string, MaterialRow> materialesPorCodigo)
    {
        var errors = new List<string>();
        string? codigoError = null;
        string? cantidadError = null;

        MaterialRow? material = null;
        bool tieneCodigo = !string.IsNullOrWhiteSpace(row.Codigo);
        if (tieneCodigo)
            materialesPorCodigo.TryGetValue(row.Codigo, out material);

        if (tieneCodigo && material == null)
            codigoError = $"Código '{row.Codigo}' no existe en el catálogo.";

        if (!string.IsNullOrWhiteSpace(codigoError))
            errors.Add(codigoError);

        if (row.Cantidad == 0m && !HasFormula(row) && tieneCodigo)
        {
            cantidadError = "Cantidad es cero.";
            errors.Add(cantidadError);
        }

        if (row.MatClpConFallback == 0m && row.MoClpConFallback == 0m && tieneCodigo)
        {
            codigoError = string.IsNullOrWhiteSpace(codigoError) ? "El precio del material es 0." : codigoError + " / El precio del material es 0.";
            errors.Add("Precio unitario es cero.");
        }

        // Antes esta alerta solo aparecía si se abría el panel derecho → Recurso → Datos
        // secundarios de la fila seleccionada; en una grilla de 50 filas era invisible
        // hasta revisarlas una por una.
        bool precioVencido = material != null && material.FechaPrecio < DateTime.Today.AddDays(-180);

        return new CubicacionRowValidation(
            codigoError,
            cantidadError,
            errors,
            errors.Count > 0,
            string.Join("\n", errors),
            precioVencido);
    }

    public void AfterRowEdited(CubicacionRow row)
    {
        _session.EnsureWritable("editar cubicación");
        _session.SyncCubicacionRowComposition(row);
    }

    public void AfterMedicionesChanged(string itemId)
    {
        _session.EnsureWritable("editar mediciones de cubicación");
        _session.ApplyCubicacionMedicionesForPartida(itemId);
    }

    public void DeleteRows(IReadOnlyCollection<string> rowIds)
    {
        if (rowIds == null || rowIds.Count == 0)
            return;

        _session.EnsureWritable("eliminar recursos de cubicación");
        _session.RemoveCubicacionRows(rowIds);
        _session.MarkDirty("cubicacion");
    }

    private static bool HasFormula(CubicacionRow row)
        => !string.IsNullOrWhiteSpace(row.TotalExpression)
           || (row.QtyExpressions != null && row.QtyExpressions.Any(e => !string.IsNullOrWhiteSpace(e)));
}
