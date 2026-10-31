using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Recurso o material unitario asignado a una tipología de departamento o recinto.
/// </summary>
public sealed class TipologiaRecursoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "un";
    public string Categoria { get; set; } = "Materiales";
    public decimal CantidadPorDepto { get; set; }
    public decimal MatClp { get; set; }
    public decimal MoClp { get; set; }
    /// <summary>
    /// Código o Item de la partida de destino en el itemizado (ej. "1.13", "1.17").
    /// </summary>
    public string PartidaCodigo { get; set; } = "";
}

/// <summary>
/// Definición de una tipología habitacional o recinto tipo (ej. Depto Tipo A 2D+2B, Studio, etc.).
/// </summary>
public sealed class TipologiaDefinicion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal SuperficieM2 { get; set; }
    public List<TipologiaRecursoItem> Items { get; set; } = new();

    public int TotalItems => Items.Count;
}

/// <summary>
/// Asignación de cantidad de departamentos de una tipología específica a un piso o zona del edificio.
/// </summary>
public sealed class TipologiaDistribucionPiso
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ZonaId { get; set; } = "";
    public string ZonaNombre { get; set; } = "";
    public string TipologiaId { get; set; } = "";
    public int CantidadDeptos { get; set; }
}

/// <summary>
/// Contenedor completo de tipologías y su matriz de distribución en el edificio.
/// </summary>
public sealed class TipologiasEdificioData
{
    public List<TipologiaDefinicion> Tipologias { get; set; } = new();
    public List<TipologiaDistribucionPiso> Distribucion { get; set; } = new();

    public bool HasContent => Tipologias.Count > 0 || Distribucion.Count > 0;

    public int TotalDepartamentos => Distribucion.Sum(d => Math.Max(0, d.CantidadDeptos));

    public static TipologiasEdificioData Default() => new();
}

/// <summary>
/// Resultado consolidado de un insumo o material para todo el edificio.
/// </summary>
public sealed class MaterialConsolidadoTipologiaDto
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string PartidaCodigo { get; set; } = "";
    public decimal CantidadTotal { get; set; }
    public decimal MatClp { get; set; }
    public decimal MoClp { get; set; }
    public decimal TotalMatClp => CantidadTotal * MatClp;
    public decimal TotalMoClp => CantidadTotal * MoClp;
    public decimal TotalClp => TotalMatClp + TotalMoClp;
}

/// <summary>
/// Resumen de una tipología con la cantidad de departamentos asignados en el edificio.
/// </summary>
public sealed class TipologiaResumenDto
{
    public string TipologiaId { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal SuperficieM2 { get; set; }
    public int CantidadDeptosTotal { get; set; }
    public int TotalRecursosDistintos { get; set; }
}
