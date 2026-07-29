using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ProyectoViewModel(
    string Nombre,
    string Cliente,
    string Ubicacion,
    string UsuarioNombre,
    string UsuarioRol,
    string Moneda,
    string EstadoPresupuesto,
    DateTime FechaCreacion,
    DateTime FechaModificacion,
    decimal ValorUfClp,
    decimal TipoCambioUsdClp,
    decimal GastosGeneralesPct,
    decimal UtilidadPct,
    decimal MetrosCuadrados,
    int Partidas,
    int Titulos,
    int Materiales,
    int LineasCubicadas,
    decimal CantidadCubicada,
    CostSummaryViewModel Costos,
    IReadOnlyList<(string Area, decimal TotalUf)> Areas);
