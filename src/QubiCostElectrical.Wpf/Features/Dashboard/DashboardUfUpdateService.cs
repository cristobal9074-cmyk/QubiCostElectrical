using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public enum UfUpdateStatus
{
    Actualizado = 0,
    SinCambios = 1,
    SoloLectura = 2
}

public sealed record UfUpdateOutcome(UfUpdateStatus Status, decimal ValorClp, DateTime Fecha);

/// <summary>
/// Aplica al proyecto la UF del día (mindicador.cl): historial de tasas, recálculo de
/// cubicación, snapshot de undo, persistencia marcada y caché global de indicadores.
/// Misma canónica de aplicación de tasas que ProjectEditWorkspaceService.Apply.
/// </summary>
public sealed class DashboardUfUpdateService
{
    private readonly BudgetProjectSession session;
    private readonly WpfUndoService? undo;
    private readonly UfRateService ufService;

    public DashboardUfUpdateService(BudgetProjectSession session, WpfUndoService? undo)
        : this(session, undo, new UfRateService()) { }

    public DashboardUfUpdateService(BudgetProjectSession session, WpfUndoService? undo, UfRateService ufService)
    {
        this.session = session;
        this.undo = undo;
        this.ufService = ufService;
    }

    public async Task<UfUpdateOutcome> ActualizarAsync()
    {
        if (session.IsReadOnly)
            return new UfUpdateOutcome(UfUpdateStatus.SoloLectura, 0m, default);

        UfRateResult uf = await ufService.GetUfHoyAsync();

        var info = session.ProjectInfo;
        if (info.ValorUfClp > 0m && Math.Abs(info.ValorUfClp - uf.ValorClp) < 0.005m)
            return new UfUpdateOutcome(UfUpdateStatus.SinCambios, uf.ValorClp, uf.Fecha);

        var before = session.CreateSnapshot();

        info.HistorialTasas ??= new();
        if (!info.HistorialTasas.Any(h =>
                h.Activa && h.UfClp == uf.ValorClp &&
                h.UsdClp == info.TipoCambioUsdClp && h.EurClp == info.TipoCambioEurClp))
        {
            foreach (var h in info.HistorialTasas) h.Activa = false;
            info.HistorialTasas.Add(new TasaCambioHistorialRow
            {
                Fecha = AppClock.Now,
                UfClp = uf.ValorClp,
                UsdClp = info.TipoCambioUsdClp,
                EurClp = info.TipoCambioEurClp,
                Activa = true
            });
            info.FechaTipoCambio = AppClock.Now;
        }

        info.ValorUfClp = uf.ValorClp;

        foreach (var row in session.Cubicacion)
            row.RecalcularUf(uf.ValorClp);

        undo?.PushSnapshot(before, "Actualizar UF al día");
        session.MarkDirty(ProjectDirtyScope.ProjectInfo);

        var cached = EconomicIndicatorCache.TryRead();
        EconomicIndicatorCache.Write(
            uf.ValorClp,
            cached is { UsdClp: > 0m } ? cached.UsdClp : info.TipoCambioUsdClp,
            AppClock.Now);

        return new UfUpdateOutcome(UfUpdateStatus.Actualizado, uf.ValorClp, uf.Fecha);
    }
}
