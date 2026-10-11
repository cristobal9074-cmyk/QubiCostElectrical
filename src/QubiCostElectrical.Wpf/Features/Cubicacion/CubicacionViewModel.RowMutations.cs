using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    // Lógica movida a CubicacionRowMutationService (ver
    // ~/.claude/plans/glittery-stargazing-hippo.md) — este archivo queda como el punto de
    // contacto público que CubicacionMedicionesVm y el resto de los partials ya conocen, sin
    // cambiar ninguna firma. Los RelayCommand del constructor apuntan directo al colaborador.

    // Expuesto para CubicacionMedicionesVm: agregar una línea de medición a una fila
    // placeholder (isSessionBacked=false, p. ej. una fila vacía en la que el usuario nunca
    // escribió código) creaba MeasurementLineRow que referenciaban un CubicacionRow.Id que
    // jamás llegaba a session.Cubicacion — una medición huérfana silenciosa que se perdía
    // (o quedaba flotando sin dueño) al cambiar de partida.
    internal void EnsureRowPersisted(CubicacionRowVm row) => rowMutationService.EnsureRowInSession(row);

    internal void EnsureRowInSession(CubicacionRowVm row) => rowMutationService.EnsureRowInSession(row);

    internal void TryAutoResolveFromPriceBank(CubicacionRowVm row) => rowMutationService.TryAutoResolveFromPriceBank(row);

    internal void ApplyMaterial(MaterialRow material) => rowMutationService.ApplyMaterial(material);

    internal static MaterialRow FromBankEntry(PriceBankEntry entry)
    {
        // Solo CLP del banco: no copiar MatUf/MoUf stale (p. ej. MoUf=1 con MoClp=1 cuando
        // ValorUF=0 hacía ClpToUf devolviendo 1, y luego alguien lo × ValorUF → MO=40.500).
        var material = new MaterialRow
        {
            Codigo = entry.Codigo,
            Descripcion = entry.Descripcion,
            Unidad = entry.Unidad,
            Categoria = entry.Categoria,
            MatClp = entry.MatClp,
            MoClp = entry.MoClp,
            MoFormulaTipo = entry.MoFormulaTipo,
            MoPorcentajeMaterial = entry.MoPorcentajeMaterial,
            MonedaPrecio = string.IsNullOrWhiteSpace(entry.MonedaPrecio) ? Moneda.CLP : entry.MonedaPrecio,
            Proveedor = entry.Proveedor,
            Marca = entry.Marca,
            OrigenPrecio = entry.OrigenPrecio,
            FechaPrecio = entry.FechaPrecio
        };
        return material;
    }

    internal void RefreshRowNumbers() => rowMutationService.RefreshRowNumbers();
}
