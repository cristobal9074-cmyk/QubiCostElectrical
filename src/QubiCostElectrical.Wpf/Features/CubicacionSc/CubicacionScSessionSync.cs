using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

/// <summary>
/// Sincroniza las colecciones y valores vivos de la grilla de circuitos SC
/// contra el objeto clon de sesión persistido tras un autoguardado.
/// </summary>
public static class CubicacionScSessionSync
{
    /// <summary>
    /// Copia artefactos y cantidades de la grilla al circuito de la sesión cuando el
    /// autoguardado ya reemplazó el objeto. El resumen y el .qce leen la sesión:
    /// sin esto, 5/8, A11 y A01 quedan en la grilla y el total de cajas sale en 0.
    /// </summary>
    public static bool SincronizarGrillaConSesion(
        IEnumerable<CircuitoScRowVm> circuitos,
        BudgetProjectSession session)
    {
        var changed = false;
        foreach (var vm in circuitos)
        {
            var live = session.CubicacionSC
                .SelectMany(c => c.Circuitos)
                .FirstOrDefault(c => string.Equals(c.Id, vm.Id, StringComparison.Ordinal));
            if (live == null || ReferenceEquals(live, vm.Source))
                continue;

            var from = vm.Source;
            if (!MismaListaArtefactos(live.Artefactos, from.Artefactos))
            {
                live.Artefactos = from.Artefactos.Select(a => new ArtefactoSCRow
                {
                    Nombre = a.Nombre,
                    Categoria = a.Categoria,
                    Cantidad = a.Cantidad,
                    ZonaId = a.ZonaId,
                }).ToList();
                changed = true;
            }

            changed |= CopiarSiDifiere(live.Cajas58, from.Cajas58, v => live.Cajas58 = v);
            changed |= CopiarSiDifiere(live.Cajas100x100, from.Cajas100x100, v => live.Cajas100x100 = v);
            changed |= CopiarSiDifiere(live.Cajas100x65, from.Cajas100x65, v => live.Cajas100x65 = v);
            changed |= CopiarSiDifiere(live.MontajesAlumbrado, from.MontajesAlumbrado, v => live.MontajesAlumbrado = v);
            changed |= CopiarSiDifiere(live.SensoresMovimiento, from.SensoresMovimiento, v => live.SensoresMovimiento = v);
            changed |= CopiarSiDifiere(live.Senaleticas, from.Senaleticas, v => live.Senaleticas = v);
            changed |= CopiarSiDifiere(live.CintaLedMts, from.CintaLedMts, v => live.CintaLedMts = v);
        }
        return changed;
    }

    private static bool CopiarSiDifiere(decimal actual, decimal visible, Action<decimal> asignar)
    {
        if (actual == visible)
            return false;
        asignar(visible);
        return true;
    }

    private static bool MismaListaArtefactos(IReadOnlyList<ArtefactoSCRow> left, IReadOnlyList<ArtefactoSCRow> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Nombre, right[i].Nombre, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(left[i].Categoria, right[i].Categoria, StringComparison.OrdinalIgnoreCase))
                return false;
            if (left[i].Cantidad != right[i].Cantidad)
                return false;
            if (!string.Equals(left[i].ZonaId ?? "", right[i].ZonaId ?? "", StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
