using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Detecta y corrige la clasificación de las líneas de cubicación que deberían ser equipo
/// (familias Tableros y Equipos) pero siguen como material. La fila marcada como equipo cambia
/// la base de ferretería, la base de gastos generales y utilidad, y activa el valor agregado
/// sobre equipos, igual que el bloque EQUIPOS de la hoja de costos.
/// </summary>
public sealed class ClasificacionEquiposService
{
    /// <summary>Líneas de familia de equipo que quedaron como material.</summary>
    public IReadOnlyList<CubicacionRow> Detectar(IEnumerable<CubicacionRow>? rows)
        => (rows ?? Enumerable.Empty<CubicacionRow>())
            .Where(r => r != null && !r.TipoManual && EsMaterial(r) && TipoRecursoFamilia.EsFamiliaEquipo(r.Categoria))
            .ToList();

    /// <summary>Costo (CLP) de las líneas detectadas: el material que está entrando como material.</summary>
    public decimal CostoDetectado(IEnumerable<CubicacionRow>? rows)
        => Detectar(rows).Sum(r => r.Cantidad * r.MatClpConFallback);

    /// <summary>Marca las líneas detectadas como equipo. Devuelve cuántas cambió.</summary>
    public int Corregir(IEnumerable<CubicacionRow>? rows)
    {
        var corregidas = 0;
        foreach (var row in Detectar(rows))
        {
            row.Tipo = TipoRecursoFamilia.Equipo;
            row.TipoManual = false;
            corregidas++;
        }
        return corregidas;
    }

    private static bool EsMaterial(CubicacionRow row)
    {
        var tipo = (row.Tipo ?? TipoRecurso.MAT).Trim().ToUpperInvariant();
        return tipo.Length == 0 || tipo == TipoRecurso.MAT;
    }
}
