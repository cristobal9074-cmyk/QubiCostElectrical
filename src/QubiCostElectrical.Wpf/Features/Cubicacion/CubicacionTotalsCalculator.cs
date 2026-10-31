using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Cálculo de los 5 totales de la partida activa (Materiales, Mano de Obra, Costo Directo,
/// Precio Unitario, Total Partida) — extraído de CubicacionViewModel.Totals.cs para que se
/// pueda testear sin construir la pantalla completa (ver plan en
/// ~/.claude/plans/glittery-stargazing-hippo.md).
///
/// Comportamiento preservado tal cual: ScheduleRefresh() espera 120ms y RECIÉN ENTONCES lee
/// la partida/filas actuales de `parent` — si el usuario cambia de partida durante ese
/// delay, el cálculo cancelado no se aplica y el siguiente lee la partida correcta. No
/// capturar la selección al llamar ScheduleRefresh() es intencional, no un descuido.
/// </summary>
internal sealed class CubicacionTotalsCalculator : IDisposable
{
    private readonly CubicacionViewModel parent;
    private readonly CubicacionPresenter cubicacionPresenter;
    private CancellationTokenSource? totalsRefreshCts;

    public CubicacionTotalsCalculator(CubicacionViewModel parent, CubicacionPresenter cubicacionPresenter)
    {
        this.parent = parent;
        this.cubicacionPresenter = cubicacionPresenter;
    }

    public void ScheduleRefresh()
    {
        totalsRefreshCts?.Cancel();
        totalsRefreshCts?.Dispose();
        totalsRefreshCts = new CancellationTokenSource();
        var token = totalsRefreshCts.Token;
        _ = RefreshAsync(token);
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(120, token);
            var selectedPartida = parent.SelectedPartida;
            if (token.IsCancellationRequested || selectedPartida == null || !selectedPartida.EsPartida)
                return;

            string partidaId = selectedPartida.Id;
            var rows = parent.Rows.Where(IsRowRelevantForTotals).Select(r => r.Source).ToList();

            var totals = await Task.Run(() =>
                cubicacionPresenter.CalculatePartidaTotals(partidaId, rows), token);

            if (token.IsCancellationRequested)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                parent.TryReevaluatePartidaCantidadFormula();
                parent.ApplyTotals(totals);
                parent.NotifyCubicacionSummaryChangedInternal();
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    public CubicacionPartidaTotals Calculate()
    {
        var selectedPartida = parent.SelectedPartida;
        if (selectedPartida == null || !selectedPartida.EsPartida)
            return new CubicacionPartidaTotals(0, 0, 0, 0, 0);

        return cubicacionPresenter.CalculatePartidaTotals(
            selectedPartida.Id,
            parent.Rows.Where(IsRowRelevantForTotals).Select(r => r.Source).ToList());
    }

    // Compartido entre Calculate() y RefreshAsync() para que ambos caminos calculen sobre
    // el mismo conjunto de filas y no den totales distintos según cuál se dispare.
    internal static bool IsRowRelevantForTotals(CubicacionRowVm r) =>
        !string.IsNullOrWhiteSpace(r.Codigo) || !string.IsNullOrWhiteSpace(r.Descripcion)
        || r.Cantidad != 0m || r.MatClp != 0m || r.MoClp != 0m
        || r.MatUf != 0m || r.MoUf != 0m;

    public void Dispose() => totalsRefreshCts?.Cancel();
}
