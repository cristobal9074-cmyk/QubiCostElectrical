using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadosPago;

/// <summary>
/// Coordina el ciclo de vida y workflow de los Estados de Pago:
/// generación de meses, clonación/duplicación con clamping, transiciones de estado
/// (Borrador -> Revisión -> Pagado / Reabrir) y consultas sobre los meses generados.
/// Fuera del ViewModel para desacoplar responsabilidades y reducir el tamaño del tipo.
/// </summary>
public sealed class EstadosPagoWorkflowCoordinator
{
    private readonly BudgetProjectSession session;
    private readonly IPermissionService permissions;

    public EstadosPagoWorkflowCoordinator(
        BudgetProjectSession session,
        IPermissionService permissions)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public EstadoPagoMesRow? FindRow(int mes)
    {
        int anio = session.EstadoPagoConfig.Anio;
        return session.EstadoPagoMeses.FirstOrDefault(r =>
            r is not null && r.Anio == anio && r.Mes == mes);
    }

    public List<EstadoPagoMesRow> GeneratedRows()
    {
        int anio = session.EstadoPagoConfig.Anio;
        return session.EstadoPagoMeses
            .Where(r => r is not null && r.Anio == anio && r.Mes >= 1)
            .OrderBy(r => r.Mes)
            .ToList();
    }

    public int MesBase(EstadoPagoMesRow? currentRow, int selectedMes)
    {
        if (currentRow is not null)
            return selectedMes;
        return LastGeneratedMes();
    }

    public int LastGeneratedMes()
    {
        var rows = GeneratedRows();
        return rows.Count > 0 ? rows[rows.Count - 1].Mes : 0;
    }

    public int FirstPendiente()
    {
        int cant = session.EstadoPagoConfig.CantMeses;
        for (int m = 1; m <= cant && m <= 12; m++)
        {
            if (FindRow(m) is null)
                return m;
        }
        return 0;
    }

    public EstadoPagoMesRow? PreviousGeneratedRow(int mes)
    {
        return GeneratedRows()
            .Where(r => r.Mes < mes)
            .LastOrDefault();
    }

    public bool GenerateMonth(
        int mes,
        EstadoPagoMesRow? duplicadoDe,
        IReadOnlyList<EstadoPagoPartidaInput> partidas,
        bool canEdit,
        out int generatedMes)
    {
        generatedMes = mes;
        if (!canEdit || mes < 1 || mes > 12)
            return false;

        var cfg = session.EstadoPagoConfig;
        if (mes > cfg.CantMeses)
        {
            WpfDialogService.Warning(
                "Estados de pago",
                $"La configuración del año tiene {cfg.CantMeses} meses: no se puede generar el estado de {EstadoPagoFormat.MesesNombres[mes - 1].ToLowerInvariant()}.");
            return false;
        }

        if (FindRow(mes) is not null)
            return false;

        var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, mes - 1);
        Dictionary<string, decimal> avances = duplicadoDe is null
            ? partidas.ToDictionary(p => p.Id, _ => 0m, StringComparer.OrdinalIgnoreCase)
            : EstadoPagoCalculator.ClampedCopy(duplicadoDe, acumAntes);

        var row = new EstadoPagoMesRow
        {
            Id = EstadoPagoMesRow.BuildId(cfg.Anio, mes),
            Anio = cfg.Anio,
            Mes = mes,
            Estado = EstadoPagoEst.Borrador,
            Folio = EstadoPagoMesRow.BuildFolio(cfg.Anio, mes),
            FechaEmision = AppClock.Now,
            AvancesPorItem = avances
        };

        session.EstadoPagoMeses.Add(row);
        session.EstadoPagoMeses.Sort((a, b) => a.Anio != b.Anio
            ? a.Anio.CompareTo(b.Anio)
            : a.Mes.CompareTo(b.Mes));
        session.MarkDirty(ProjectDirtyScope.EstadosPago);

        generatedMes = mes;
        return true;
    }

    public bool Aprobar(EstadoPagoMesRow? currentRow, bool canApprove)
    {
        if (!canApprove || currentRow is null || currentRow.Estado != EstadoPagoEst.Borrador)
            return false;
        currentRow.Estado = EstadoPagoEst.Revision;
        currentRow.AprobadoPor = session.ProjectInfo.UsuarioNombre;
        currentRow.FechaAprobacion = AppClock.Now;
        session.MarkDirty(ProjectDirtyScope.EstadosPago);
        WpfDialogService.Info(
            "Estado aprobado",
            $"El estado {currentRow.Folio} pasó a Revisión. Un administrador puede registrarlo como pagado.");
        return true;
    }

    public bool RegistrarPago(EstadoPagoMesRow? currentRow, bool canApprove)
    {
        if (!canApprove || currentRow is null || currentRow.Estado != EstadoPagoEst.Revision)
            return false;
        currentRow.Estado = EstadoPagoEst.Pago;
        if (string.IsNullOrWhiteSpace(currentRow.AprobadoPor))
        {
            currentRow.AprobadoPor = session.ProjectInfo.UsuarioNombre;
            currentRow.FechaAprobacion = AppClock.Now;
        }
        session.MarkDirty(ProjectDirtyScope.EstadosPago);
        WpfDialogService.Info(
            "Pago registrado",
            $"El estado {currentRow.Folio} quedó registrado como Pagado.");
        return true;
    }

    public bool Reabrir(EstadoPagoMesRow? currentRow, bool canApprove)
    {
        if (!canApprove || currentRow is null || currentRow.Estado == EstadoPagoEst.Borrador)
            return false;
        currentRow.Estado = EstadoPagoEst.Borrador;
        currentRow.AprobadoPor = "";
        currentRow.FechaAprobacion = null;
        session.MarkDirty(ProjectDirtyScope.EstadosPago);
        WpfDialogService.Info(
            "Estado reabierto",
            $"El estado {currentRow.Folio} volvió a Borrador y puede editarse.");
        return true;
    }

    public void RecomputeMonthTotals(
        IReadOnlyList<EstadoPagoPartidaInput> partidas,
        IDictionary<int, EstadoPagoMesCalc> totalesPorMes)
    {
        totalesPorMes.Clear();
        var cfg = session.EstadoPagoConfig;
        foreach (var row in GeneratedRows())
        {
            var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, cfg.Anio, row.Mes - 1);
            var detalle = EstadoPagoCalculator.CalcularMes(partidas, acumAntes, row.AvancesPorItem, cfg);
            totalesPorMes[row.Mes] = detalle.Totales;
        }
    }
}
