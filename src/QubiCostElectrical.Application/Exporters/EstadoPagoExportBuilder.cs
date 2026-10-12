using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Construye el modelo de exportación de un estado de pago a partir del cálculo puro
/// (<see cref="EstadoPagoCalculator"/>): agrupa las partidas por capítulo, calcula los
/// totales del mes seleccionado y arma el cronograma anual. Sin IO ni dependencias de UI.
/// </summary>
public static class EstadoPagoExportBuilder
{
    /// <summary>
    /// Devuelve <c>null</c> si <paramref name="row"/> es nulo (mes aún no generado).
    /// </summary>
    public static EstadoPagoExportRequest? Build(
        string projectTitle,
        EstadoPagoConfig config,
        EstadoPagoMesRow? row,
        IReadOnlyList<EstadoPagoExportSeccionInput> secciones,
        IEnumerable<EstadoPagoMesRow> meses,
        string exportadoPor,
        DateTime exportadoAt,
        string monedaPresupuesto = Moneda.CLP,
        decimal valorUfClp = 0m)
    {
        if (row is null)
            return null;

        config ??= EstadoPagoConfig.Default();
        secciones ??= Array.Empty<EstadoPagoExportSeccionInput>();
        meses ??= Array.Empty<EstadoPagoMesRow>();

        var todas = secciones.SelectMany(s => s.Partidas).ToList();
        var acumAntes = EstadoPagoCalculator.BuildAcumulado(meses, config.Anio, row.Mes - 1);
        var detalle = EstadoPagoCalculator.CalcularMes(todas, acumAntes, row.AvancesPorItem, config);

        var mes = new EstadoPagoExportMes(
            row.Anio,
            row.Mes,
            EstadoPagoExportText.MesNombre(row.Mes),
            row.Folio,
            row.FechaEmision,
            row.Estado,
            EstadoPagoExportText.EstadoLabel(row.Estado),
            row.AprobadoPor,
            BuildSecciones(secciones, detalle.Filas),
            detalle.Totales);

        var cronograma = BuildCronograma(config, meses, todas);

        return new EstadoPagoExportRequest(
            string.IsNullOrWhiteSpace(projectTitle) ? "Proyecto sin nombre" : projectTitle,
            config,
            mes,
            cronograma,
            exportadoPor ?? "",
            exportadoAt,
            monedaPresupuesto,
            valorUfClp);
    }

    private static IReadOnlyList<EstadoPagoExportSeccion> BuildSecciones(
        IReadOnlyList<EstadoPagoExportSeccionInput> secciones,
        IReadOnlyList<EstadoPagoPartidaCalc> filas)
    {
        var porId = filas.ToDictionary(f => f.ItemId, StringComparer.OrdinalIgnoreCase);
        var resultado = new List<EstadoPagoExportSeccion>(secciones.Count);

        foreach (var sec in secciones)
        {
            var exportFilas = new List<EstadoPagoExportFila>(sec.Partidas.Count);
            foreach (var p in sec.Partidas)
            {
                if (!porId.TryGetValue(p.Id, out var f))
                    continue;
                exportFilas.Add(new EstadoPagoExportFila(
                    f.Item, f.Descripcion, f.Monto, f.AvanceAnterior, f.AvanceMes, f.MontoMes, f.AcumPct));
            }

            if (exportFilas.Count > 0)
                resultado.Add(new EstadoPagoExportSeccion(sec.Capitulo, exportFilas));
        }

        return resultado;
    }

    private static IReadOnlyList<EstadoPagoExportCronogramaMes> BuildCronograma(
        EstadoPagoConfig config,
        IEnumerable<EstadoPagoMesRow> meses,
        List<EstadoPagoPartidaInput> partidas)
    {
        var porMes = meses
            .Where(r => r is not null && r.Anio == config.Anio && r.Mes >= 1 && r.Mes <= 12)
            .GroupBy(r => r.Mes)
            .ToDictionary(g => g.Key, g => g.Last());

        var lista = new List<EstadoPagoExportCronogramaMes>(12);
        for (int m = 1; m <= 12; m++)
        {
            if (!porMes.TryGetValue(m, out var row))
            {
                lista.Add(new EstadoPagoExportCronogramaMes(
                    m, EstadoPagoExportText.MesNombre(m), "", "", EstadoPagoExportText.EstadoLabel(null), null));
                continue;
            }

            var acumAntes = EstadoPagoCalculator.BuildAcumulado(meses, config.Anio, row.Mes - 1);
            var detalle = EstadoPagoCalculator.CalcularMes(partidas, acumAntes, row.AvancesPorItem, config);
            lista.Add(new EstadoPagoExportCronogramaMes(
                m,
                EstadoPagoExportText.MesNombre(m),
                row.Folio,
                row.Estado,
                EstadoPagoExportText.EstadoLabel(row.Estado),
                detalle.Totales.Total));
        }

        return lista;
    }
}
