using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Coordina la asignación y gestión de familias de precio, catálogo interactivo y chips en la pantalla de Costos.
/// </summary>
internal sealed class CostoFamilyPricingCoordinator
{
    private readonly CostoViewModel _vm;

    public CostoFamilyPricingCoordinator(CostoViewModel vm)
    {
        _vm = vm;
    }

    public void MovePrecioFamilia(PrecioFamiliaChipVm? chip, string? targetFamilyKey)
    {
        if (!_vm.CanEdit || chip == null)
            return;

        var target = _vm.CoefFactors.FirstOrDefault(f =>
            string.Equals(f.FamilyKey, targetFamilyKey, StringComparison.OrdinalIgnoreCase));

        _vm.PrecioFamiliasLibres.Remove(chip);
        foreach (var row in _vm.CoefFactors)
            row.FamiliasPrecio.Remove(chip);

        if (target == null)
            _vm.PrecioFamiliasLibres.Add(chip);
        else
            target.FamiliasPrecio.Add(chip);

        _vm.NotifyHasPrecioFamiliasLibresChanged();

        _vm.Session.ProjectInfo.FamiliasPrecioAsignadas ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _vm.Session.ProjectInfo.FamiliasPrecioAsignadas[chip.Clave] = target?.FamilyKey ?? "";

        _vm.SuppressCoefRefresh = true;
        try
        {
            _vm.Session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            _vm.Session.SyncCubicacionGgUtFactorsFromVariables();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    public void RebuildPrecioFamiliaChips()
    {
        foreach (var row in _vm.CoefFactors)
            row.FamiliasPrecio.Clear();
        _vm.PrecioFamiliasLibres.Clear();

        var map = _vm.Session.ProjectInfo.FamiliasPrecioAsignadas;
        var familiasActivas = EnumerarFamiliasPrecio();
        var clavesActivas = new HashSet<string>(familiasActivas.Select(FamilyPresentationDefaults.Normalize), StringComparer.OrdinalIgnoreCase);

        if (map != null && map.Count > 0)
        {
            var huerfanas = map.Keys.Where(k => !clavesActivas.Contains(k)).ToList();
            if (huerfanas.Count > 0)
            {
                foreach (var h in huerfanas)
                    map.Remove(h);
                _vm.Session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            }
        }

        foreach (var nombre in familiasActivas)
        {
            var clave = FamilyPresentationDefaults.Normalize(nombre);
            var chip = new PrecioFamiliaChipVm(nombre, clave);
            var row = FilaDeFamiliaPrecio(clave, map);
            if (row == null)
                _vm.PrecioFamiliasLibres.Add(chip);
            else
                row.FamiliasPrecio.Add(chip);
            PublicarEstadoChip(chip, nombre);
        }

        _vm.NotifyHasPrecioFamiliasLibresChanged();
        foreach (var row in _vm.CoefFactors)
            row.RefrescarResumenFamilias();
    }

    public void AbrirGestionFamilias()
    {
        if (!_vm.CanEdit)
            return;

        var vm = new GestionFamiliasViewModel(_vm.Session, _vm.PriceBank, () =>
        {
            _vm.SuppressCoefRefresh = true;
            try
            {
                _vm.RefreshCostOnly();
                RebuildPrecioFamiliaChips();
                _vm.NotifyFamiliasChanged();
            }
            finally
            {
                _vm.SuppressCoefRefresh = false;
            }
        });

        var owner = Application.Current?.MainWindow;
        var dialog = new GestionFamiliasDialog(vm);
        if (owner != null)
            dialog.Owner = owner;

        dialog.ShowDialog();

        _vm.RefreshCostOnly();
        RebuildPrecioFamiliaChips();
        _vm.NotifyFamiliasChanged();
    }

    public void AbrirCatalogoFamilias(CoefFactorVm? fila)
    {
        if (fila == null || !_vm.IsFactoresFamiliaEditable)
            return;

        var abrir = !fila.CatalogoAbierto;
        foreach (var row in _vm.CoefFactors)
            row.CatalogoAbierto = false;
        if (!abrir)
            return;

        LlenarCatalogoFamilias(fila);
        fila.CatalogoAbierto = true;
    }

    public void ElegirFamiliaPrecio(PrecioFamiliaPickVm? pick)
    {
        if (pick == null || pick.Bloqueada || !_vm.IsFactoresFamiliaEditable)
            return;

        var fila = _vm.CoefFactors.FirstOrDefault(f => f.CatalogoAbierto);
        if (fila == null)
            return;

        var chip = _vm.PrecioFamiliasLibres.FirstOrDefault(c => MismaFamilia(c, pick))
            ?? _vm.CoefFactors.SelectMany(r => r.FamiliasPrecio).FirstOrDefault(c => MismaFamilia(c, pick))
            ?? new PrecioFamiliaChipVm(pick.Nombre, pick.Clave);
        MovePrecioFamilia(chip, pick.EnEstaFila ? null : fila.FamilyKey);
        if (fila.CatalogoAbierto)
            LlenarCatalogoFamilias(fila);
    }

    public void LlenarCatalogoFamilias(CoefFactorVm fila)
    {
        fila.Catalogo.Clear();
        foreach (var nombre in EnumerarFamiliasPrecio())
        {
            var clave = FamilyPresentationDefaults.Normalize(nombre);
            CoefFactorVm? duena = null;
            foreach (var row in _vm.CoefFactors)
            {
                if (row.FamiliasPrecio.Any(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase)))
                {
                    duena = row;
                    break;
                }
            }

            var enEsta = ReferenceEquals(duena, fila);
            fila.Catalogo.Add(new PrecioFamiliaPickVm(
                nombre,
                clave,
                enEsta,
                duena != null && !enEsta,
                duena?.Name ?? ""));
        }
    }

    public static bool MismaFamilia(PrecioFamiliaChipVm chip, PrecioFamiliaPickVm pick)
        => string.Equals(chip.Clave, pick.Clave, StringComparison.OrdinalIgnoreCase);

    public CoefFactorVm? FilaDeFamiliaPrecio(string clave, Dictionary<string, string>? map)
    {
        if (map != null && map.TryGetValue(clave, out var assigned))
        {
            if (string.IsNullOrWhiteSpace(assigned))
                return null;
            return _vm.CoefFactors.FirstOrDefault(f =>
                string.Equals(f.FamilyKey, assigned, StringComparison.OrdinalIgnoreCase));
        }

        var direct = _vm.CoefFactors.FirstOrDefault(f =>
            string.Equals(FamilyPresentationDefaults.Normalize(f.FamilyKey), clave, StringComparison.Ordinal));
        if (direct != null)
            return direct;

        var naturalTarget = FamilyPresentationDefaults.NaturalCanonicalTarget(clave);
        return _vm.CoefFactors.FirstOrDefault(f =>
            string.Equals(FamilyPresentationDefaults.Normalize(f.FamilyKey), naturalTarget, StringComparison.Ordinal));
    }

    public List<string> EnumerarFamiliasPrecio()
    {
        var bankCategories = (_vm.PriceBank?.GetAll().Select(e => e.Categoria) ?? Enumerable.Empty<string>()).ToList();
        return ElectricalFamilyCatalog.CombinarFamiliasConOcultas(
            _vm.Session.ProjectInfo.FamiliasOcultas,
            _vm.Session.ProjectInfo.FamiliasPersonalizadas,
            bankCategories).ToList();
    }

    /// <summary>
    /// Publica el factor y el origen de cada familia para que el chip diga de donde sale su valor
    /// (propio, heredado de otra fila, o sin configurar).
    /// </summary>
    private void PublicarEstadoChip(PrecioFamiliaChipVm chip, string nombre)
    {
        var informe = _vm.InformeFamilias;
        if (informe == null)
        {
            chip.SetEstado(null, false);
            return;
        }

        var info = informe.Resolver(nombre);
        var usada = informe.Familias.Any(f =>
            string.Equals(f.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        chip.SetEstado(info, usada);
    }

    public void UpdateCoefChips()
    {
        if (_vm.CoefFactors.Count == 0)
        {
            _vm.CoefFactorMedioLabel = "1,00×";
            _vm.CoefActivasLabel = "0/0";
            return;
        }

        decimal sum = 0m;
        int active = 0;
        foreach (var f in _vm.CoefFactors)
        {
            var v = _vm.Session.GetFamilyPresentationFactor(f.FamilyKey);
            sum += v;
            if (v > 1m) active++;
        }

        _vm.CoefFactorMedioLabel = $"{sum / _vm.CoefFactors.Count:N2}×";
        _vm.CoefActivasLabel = $"{active}/{_vm.CoefFactors.Count}";
    }
}
