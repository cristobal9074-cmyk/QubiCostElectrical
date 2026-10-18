using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Coordinador de cabecera de partida activa: propiedades de identificación, cantidad, unidad, observaciones y alertas globales.
/// </summary>
internal sealed class CubicacionPartidaCoordinator
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly WpfUndoService undo;

    public CubicacionPartidaCoordinator(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        WpfUndoService undo)
    {
        this.parent = parent;
        this.session = session;
        this.undo = undo;
    }

    public string PartidaEstadoText
    {
        get
        {
            if (!parent.HasPartida)
                return "Sin partida seleccionada";
            if (HasPartidaNote)
                return MotivoNoCubicarText.Trim();
            return parent.SelectedPartida?.TieneCubicacion == true ? "Partida cubicada" : "Partida vacía";
        }
    }

    public bool HasPartidaNote => !string.IsNullOrWhiteSpace(MotivoNoCubicarText);

    public string PartidaNoteLabel => HasPartidaNote ? "Observación:" : "";

    public bool ExcluidoCubicacion
    {
        get => parent.SelectedPartida?.ExcluidoCubicacion == true;
        set
        {
            if (session.IsReadOnly || parent.SelectedPartida?.EsPartida != true)
                return;

            var item = session.Itemizado.FirstOrDefault(x =>
                string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null || item.ExcluidoCubicacion == value)
                return;

            undo.PushUndoPoint(session,
                value ? $"Excluir de cubicación {parent.SelectedPartida.Codigo}" : $"Incluir en cubicación {parent.SelectedPartida.Codigo}");
            item.ExcluidoCubicacion = value;
            parent.SelectedPartida.ExcluidoCubicacion = value;
            parent.MarkDirty();
            parent.NotifyProperty(nameof(parent.ExcluidoCubicacion));
            parent.NotifyProperty(nameof(parent.IsReadOnly));
            parent.NotifyProperty(nameof(parent.PartidaEstadoText));
            ((RelayCommand)parent.AddRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)parent.InsertRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)parent.AplicarConjuntoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)parent.CrearConjuntoDesdePartidaCommand).RaiseCanExecuteChanged();
            ((RelayCommand)parent.OpenScTomarCommand).RaiseCanExecuteChanged();
        }
    }

    public string MotivoNoCubicarText
    {
        get
        {
            if (parent.SelectedPartida == null)
                return "";
            var item = session.Itemizado.FirstOrDefault(x =>
                string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            return item?.MotivoNoCubicar ?? "";
        }
        set
        {
            if (session.IsReadOnly || parent.SelectedPartida?.EsPartida != true)
                return;
            var item = session.Itemizado.FirstOrDefault(x =>
                string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return;

            string clean = value ?? "";
            if (string.Equals(item.MotivoNoCubicar, clean, StringComparison.Ordinal))
                return;

            undo.PushUndoPoint(session, $"Observación partida {parent.SelectedPartida.Codigo}");
            item.MotivoNoCubicar = clean;
            if (!string.IsNullOrWhiteSpace(clean))
            {
                ObservationMemoryStore.Remember(UserPreferencesStore.Load(), clean);
            }

            parent.NotifyProperty(nameof(parent.MotivoNoCubicarText));
            parent.NotifyProperty(nameof(parent.HasPartidaNote));
            parent.NotifyProperty(nameof(parent.PartidaNoteLabel));
            parent.NotifyProperty(nameof(parent.PartidaEstadoText));
            parent.MarkDirty();
        }
    }

    public IReadOnlyList<ObservationChipPreset> ObservationChips => ObservationMemoryStore.StandardChips;

    public void ApplyObservationChip(string text)
    {
        if (session.IsReadOnly || parent.SelectedPartida?.EsPartida != true)
            return;

        MotivoNoCubicarText = text;
        parent.SaveStatusText = $"Observación aplicada: '{text}'.";
    }

    public void ClearObservation()
    {
        if (session.IsReadOnly || parent.SelectedPartida?.EsPartida != true)
            return;

        MotivoNoCubicarText = string.Empty;
        parent.SaveStatusText = "Observación eliminada.";
    }

    public string PartidaResumenText => !parent.HasPartida
        ? "Seleccione una partida para ver el detalle"
        : $"{parent.Rows.Count(r => r.HasContent):N0} recursos · $ {parent.TotalPartida:N0} CLP";

    public string PartidaCodigo
    {
        get => parent.SelectedPartida?.Codigo ?? "";
        set
        {
            if (session.IsReadOnly || parent.SelectedPartida == null) return;
            string clean = (value ?? "").Trim();
            if (parent.SelectedPartida.Codigo == clean) return;

            undo.PushUndoPoint(session, $"Cambiar ítem partida {parent.SelectedPartida.Codigo} a {clean}");
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item != null)
                item.Item = clean;

            parent.SelectedPartida.Codigo = clean;
            parent.NotifyProperty(nameof(parent.PartidaCodigo));
            parent.NotifyProperty(nameof(parent.PartidaPositionText));
            parent.MarkDirty();
            session.MarkDirty(ProjectDirtyScope.Itemizado);
        }
    }

    public string PartidaDescripcion
    {
        get => parent.SelectedPartida?.Descripcion ?? "";
        set
        {
            if (session.IsReadOnly || parent.SelectedPartida == null) return;
            string clean = (value ?? "").Trim();
            if (parent.SelectedPartida.Descripcion == clean) return;

            undo.PushUndoPoint(session, $"Cambiar nombre partida {parent.SelectedPartida.Codigo}");
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item != null)
                item.Descripcion = clean;

            parent.SelectedPartida.Descripcion = clean;
            parent.NotifyProperty(nameof(parent.PartidaDescripcion));
            parent.MarkDirty();
            session.MarkDirty(ProjectDirtyScope.Itemizado);
        }
    }

    public string PartidaUnidad => parent.SelectedPartida?.Unidad ?? "";

    public string PartidaUnidadText
    {
        get => PartidaUnidad;
        set
        {
            if (session.IsReadOnly) return;
            if (parent.SelectedPartida == null) return;
            string clean = (value ?? "").Trim();
            clean = ResolveUnidadReference(clean);
            if (parent.SelectedPartida.Unidad == clean) return;

            using (parent.SuppressSessionReload())
            {
                undo.PushUndoPoint(session, $"Cambiar unidad partida {parent.SelectedPartida.Codigo}");
                var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
                if (item != null) item.Unidad = clean;
                parent.SelectedPartida.Unidad = clean;
                parent.NotifyProperty(nameof(parent.PartidaUnidadText));
                parent.NotifyProperty(nameof(parent.PartidaUnidad));
                NotifyGlobalValorizacionChanged();
                parent.MarkDirty();
                session.MarkDirty(ProjectDirtyScope.Itemizado);
            }
        }
    }

    public string ResolveUnidadReference(string input)
        => CubicacionPartidaHelper.ResolveUnidadReference(input, parent.Rows);

    public string PartidaPositionText
    {
        get
        {
            if (parent.SelectedPartida == null) return "";
            var partidas = parent.FilteredPartidas.Where(p => p.EsPartida).ToList();
            int idx = partidas.IndexOf(parent.SelectedPartida);
            return idx >= 0 ? $"Partida {idx + 1}/{partidas.Count}" : "";
        }
    }

    public string PartidaCantidadText
    {
        get
        {
            if (parent.SelectedPartida == null) return "";
            return parent.SelectedPartida.Cantidad % 1 == 0
                ? parent.SelectedPartida.Cantidad.ToString("N0", CultureInfo.CurrentCulture)
                : parent.SelectedPartida.Cantidad.ToString("#,##0.##", CultureInfo.CurrentCulture);
        }
        set
        {
            if (session.IsReadOnly || parent.SelectedPartida == null) return;
            string clean = (value ?? "").Trim();
            if (!CubicacionPartidaHelper.TryParsePartidaCantidad(clean, BuildPartidaCantidadVariables(), out decimal parsed, out string formula))
            {
                parent.NotifyProperty(nameof(parent.PartidaCantidadText));
                parent.NotifyProperty(nameof(parent.PartidaCantidadToolTip));
                NotifyGlobalValorizacionChanged();
                return;
            }

            if (!string.IsNullOrEmpty(formula)) parent.SelectedPartida.CantidadFormula = formula;
            else if (parent.SelectedPartida.Cantidad != parsed || string.IsNullOrEmpty(parent.SelectedPartida.CantidadFormula)) parent.SelectedPartida.CantidadFormula = "";

            using (parent.SuppressSessionReload())
            {
                var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
                if (item != null) item.Cantidad = parsed;
                if (parent.SelectedPartida.Cantidad != parsed)
                {
                    undo.PushUndoPoint(session, $"Modificar cantidad en Partida {parent.SelectedPartida.Codigo} ({parent.SelectedPartida.Cantidad:G29} → {parsed:G29})");
                    parent.SelectedPartida.Cantidad = parsed;
                }
                CubicacionPartidaHelper.SincronizarMaestroDesdePartida(parent.Rows, parsed, session, parent.ValidateRow);
                parent.NotifyProperty(nameof(parent.PartidaCantidadText));
                parent.NotifyProperty(nameof(parent.PartidaCantidadToolTip));
                NotifyGlobalValorizacionChanged();
                parent.MarkDirty();
                session.MarkDirty(ProjectDirtyScope.Itemizado);
                parent.ScheduleRefreshTotals();
            }
        }
    }

    public string PartidaCantidadToolTip => parent.SelectedPartida?.CantidadToolTip ?? "Cantidad oficial de la partida (editable o fórmula tipo Excel ej: +C1+C2 · Enter para guardar)";

    public bool HasAlertaSinValorizacionGlobal => parent.SelectedPartida is { SinValorizacionGlobal: true };
    public bool IsPartidaUnidadMissing => parent.SelectedPartida is { HasMissingGlobalUnidad: true };
    public bool IsPartidaCantidadMissing => parent.SelectedPartida is { HasMissingGlobalCantidad: true };
    public string AlertaSinValorizacionGlobalMensaje => CubicacionPartidaHelper.GetAlertaMensaje(parent.SelectedPartida);
    public string AlertaSinValorizacionGlobalBadgeText
    {
        get
        {
            if (parent.SelectedPartida == null || !parent.SelectedPartida.SinValorizacionGlobal) return "";
            if (parent.SelectedPartida.HasMissingGlobalUnidad && parent.SelectedPartida.HasMissingGlobalCantidad)
                return "Sin unidad ni cantidad";
            if (parent.SelectedPartida.HasMissingGlobalCantidad)
                return "Sin cantidad global";
            return "Sin unidad global";
        }
    }

    public void NotifyGlobalValorizacionChanged()
    {
        parent.NotifyProperty(nameof(parent.HasAlertaSinValorizacionGlobal));
        parent.NotifyProperty(nameof(parent.AlertaSinValorizacionGlobalMensaje));
        parent.NotifyProperty(nameof(parent.AlertaSinValorizacionGlobalBadgeText));
        parent.NotifyProperty(nameof(parent.IsPartidaUnidadMissing));
        parent.NotifyProperty(nameof(parent.IsPartidaCantidadMissing));
    }

    public Dictionary<string, decimal> BuildPartidaCantidadVariables()
        => CubicacionPartidaHelper.BuildVariables(parent.Rows, session);

    public string PartidaObservacionesText
    {
        get
        {
            var item = parent.SelectedPartida == null
                ? null
                : session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            return item?.Observaciones ?? "";
        }
        set
        {
            if (session.IsReadOnly) return;
            if (parent.SelectedPartida == null) return;
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, parent.SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null) return;

            string clean = value ?? "";
            if (item.Observaciones == clean) return;

            undo.PushUndoPoint(session, $"Editar observación de {parent.SelectedPartida.Codigo}");
            item.Observaciones = clean;
            parent.NotifyProperty(nameof(parent.PartidaObservacionesText));
            parent.MarkDirty();
        }
    }
}
