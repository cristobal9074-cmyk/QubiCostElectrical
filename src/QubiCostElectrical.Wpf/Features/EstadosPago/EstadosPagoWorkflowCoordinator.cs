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
/// Soporta obras iniciadas en cualquier mes del año y cronogramas multi-año.
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

    public EstadoPagoMesRow? FindRow(int anio, int mes)
    {
        return session.EstadoPagoMeses.FirstOrDefault(r =>
            r is not null && r.Anio == anio && r.Mes == mes);
    }

    public EstadoPagoMesRow? FindRow(int mes) => FindRow(session.EstadoPagoConfig.Anio, mes);

    public List<EstadoPagoMesRow> GeneratedRows()
    {
        return session.EstadoPagoMeses
            .Where(r => r is not null && r.Mes >= 1 && r.Mes <= 12)
            .OrderBy(r => r.Anio * 12 + r.Mes)
            .ToList();
    }

    public EstadoPagoMesRow? LastGeneratedRow()
    {
        var rows = GeneratedRows();
        return rows.Count > 0 ? rows[rows.Count - 1] : null;
    }

    public (int Anio, int Mes) PeriodoBase(EstadoPagoMesRow? currentRow, int selectedAnio, int selectedMes)
    {
        if (currentRow is not null)
            return (selectedAnio, selectedMes);
        var last = LastGeneratedRow();
        return last is not null ? (last.Anio, last.Mes) : (selectedAnio, 0);
    }

    public int MesBase(EstadoPagoMesRow? currentRow, int selectedMes)
    {
        if (currentRow is not null)
            return selectedMes;
        var last = LastGeneratedRow();
        return last?.Mes ?? 0;
    }

    public int LastGeneratedMes()
    {
        var last = LastGeneratedRow();
        return last?.Mes ?? 0;
    }

    public (int Anio, int Mes)? FirstPendiente()
    {
        var cfg = session.EstadoPagoConfig;
        int cant = Math.Max(1, cfg.CantMeses);
        for (int i = 0; i < cant; i++)
        {
            var (a, m) = EstadoPagoCalculator.GetMonthForPeriod(cfg, i);
            if (FindRow(a, m) is null)
                return (a, m);
        }
        return null;
    }

    public EstadoPagoMesRow? PreviousGeneratedRow(int anio, int mes)
    {
        int targetKey = anio * 12 + mes;
        return GeneratedRows()
            .Where(r => (r.Anio * 12 + r.Mes) < targetKey)
            .LastOrDefault();
    }

    public EstadoPagoMesRow? PreviousGeneratedRow(int mes) =>
        PreviousGeneratedRow(session.EstadoPagoConfig.Anio, mes);

    public bool GenerateMonth(
        int anio,
        int mes,
        EstadoPagoMesRow? duplicadoDe,
        IReadOnlyList<EstadoPagoPartidaInput> partidas,
        bool canEdit,
        out EstadoPagoMesRow? createdRow)
    {
        createdRow = null;
        if (!canEdit || mes < 1 || mes > 12)
            return false;

        var cfg = session.EstadoPagoConfig;
        if (FindRow(anio, mes) is not null)
            return false;

        var (prevAnio, prevMes) = EstadoPagoCalculator.PreviousMonth(anio, mes);
        var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, prevAnio, prevMes, cfg.Anio);
        Dictionary<string, decimal> avances = duplicadoDe is null
            ? partidas.ToDictionary(p => p.Id, _ => 0m, StringComparer.OrdinalIgnoreCase)
            : EstadoPagoCalculator.ClampedCopy(duplicadoDe, acumAntes);

        var row = new EstadoPagoMesRow
        {
            Id = EstadoPagoMesRow.BuildId(anio, mes),
            Anio = anio,
            Mes = mes,
            Estado = EstadoPagoEst.Borrador,
            Folio = EstadoPagoMesRow.BuildFolio(anio, mes),
            FechaEmision = AppClock.Now,
            AvancesPorItem = avances
        };

        session.EstadoPagoMeses.Add(row);
        session.EstadoPagoMeses.Sort((a, b) => a.Anio != b.Anio
            ? a.Anio.CompareTo(b.Anio)
            : a.Mes.CompareTo(b.Mes));
        session.MarkDirty(ProjectDirtyScope.EstadosPago);

        createdRow = row;
        return true;
    }

    public bool GenerateMonth(
        int mes,
        EstadoPagoMesRow? duplicadoDe,
        IReadOnlyList<EstadoPagoPartidaInput> partidas,
        bool canEdit,
        out int generatedMes)
    {
        int anio = session.EstadoPagoConfig.Anio;
        bool ok = GenerateMonth(anio, mes, duplicadoDe, partidas, canEdit, out var row);
        generatedMes = row?.Mes ?? mes;
        return ok;
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
            var (prevAnio, prevMes) = EstadoPagoCalculator.PreviousMonth(row.Anio, row.Mes);
            var acumAntes = EstadoPagoCalculator.BuildAcumulado(session.EstadoPagoMeses, prevAnio, prevMes, cfg.Anio);
            var detalle = EstadoPagoCalculator.CalcularMes(partidas, acumAntes, row.AvancesPorItem, cfg);
            totalesPorMes[row.Mes] = detalle.Totales;
        }
    }
}
