using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionRightPanelVm : ViewModelBase
{
    public CubicacionRightPanelVm(BudgetProjectSession session, CubicacionViewModel parent)
    {
        this.session = session;
        this.parent = parent;
        Mediciones = new CubicacionMedicionesVm(session, parent);

        // Inicializar los 300 ítems una sola vez para la lista maestra
        for (int i = 0; i < 300; i++)
        {
            allZonesMaster.Add(new ZoneDistributionItem(this, i + 1, "", string.Empty));
        }
    }

    public CubicacionMedicionesVm Mediciones { get; }

    public CubicacionRowVm? CurrentRow => currentRow;
    public bool HasCurrentRow => currentRow != null;
    public string CurrentRowTitle => currentRow == null
        ? "Sin recurso seleccionado"
        : string.IsNullOrWhiteSpace(currentRow.Codigo) && string.IsNullOrWhiteSpace(currentRow.Descripcion)
            ? "Fila nueva — sin código ni descripción"
            : $"{currentRow.Codigo} · {currentRow.Descripcion}";
    public string CurrentRowSubtitle => currentRow == null
        ? "Seleccione una fila para ver el contexto, las mediciones y los controles."
        : $"{currentRow.Unidad} · Cant. {currentRow.CantidadDisplay} · Área {currentRow.Area}";
    public string CurrentRowStatus => currentRow == null
        ? "Contexto"
        : HasAlertas ? $"Revisar · {Alertas.Count} alertas" : (currentRow.HasFormula ? "Con fórmula" : "Correcto");
    public string CurrentRowTotals => currentRow == null
        ? "—"
        : $"Mat $ {currentRow.MatClpDisplay} · MO $ {currentRow.MoClpDisplay} · $ UNIT {currentRow.PrecioUnitClpDisplay} · Subtotal $ {currentRow.SubtotalClpDisplay}";

    public string ActiveTab
    {
        get => activeTab;
        set => SetProperty(ref activeTab, value);
    }

    public string Comentario
    {
        get => comentario;
        set
        {
            if (SetProperty(ref comentario, value) && currentRow != null)
            {
                SaveComment(currentRow.Source, value);
                PersistCurrentRowEdit();
            }
        }
    }

    public string Zona
    {
        get => zona;
        set
        {
            if (SetProperty(ref zona, value) && currentRow != null)
            {
                currentRow.Area = value;
                OnPropertyChanged(nameof(CurrentRowSubtitle));
                PersistCurrentRowEdit();
            }
        }
    }

    public string RedondeoModo
    {
        get => redondeoModo;
        set
        {
            if (SetProperty(ref redondeoModo, value) && currentRow != null)
            {
                currentRow.Source.RedondeoModo = value;
                PersistCurrentRowEdit();
            }
        }
    }

    public decimal RedondeoMultiplo
    {
        get => redondeoMultiplo;
        set
        {
            if (SetProperty(ref redondeoMultiplo, value) && currentRow != null)
            {
                currentRow.Source.RedondeoMultiplo = value;
                PersistCurrentRowEdit();
            }
        }
    }

    public decimal MermaPctOverride
    {
        get => mermaPctOverride;
        set
        {
            if (SetProperty(ref mermaPctOverride, value) && currentRow != null)
            {
                currentRow.Source.MermaPctOverride = value;
                PersistCurrentRowEdit();
            }
        }
    }

    public decimal HerramientasPctOverride
    {
        get => herramientasPctOverride;
        set
        {
            if (SetProperty(ref herramientasPctOverride, value) && currentRow != null)
            {
                currentRow.Source.HerramientasPctOverride = value;
                PersistCurrentRowEdit();
            }
        }
    }

    // Los setters de arriba mutaban currentRow.Source directamente pero nunca avisaban al
    // ViewModel dueño: el proyecto no quedaba "con cambios sin guardar", los totales no se
    // recalculaban, y si la fila era un placeholder aún no persistido (isSessionBacked=false)
    // el cambio se perdía al cambiar de partida. OnRowEdited hace todo eso de una vez.
    private void PersistCurrentRowEdit()
    {
        if (currentRow != null)
            parent.OnRowEdited(currentRow);
    }

    public string MermaPctOverrideDisplay => mermaPctOverride < 0 ? "(hereda proyecto)" : $"{mermaPctOverride:N2}%";
    public string HerramientasPctOverrideDisplay => herramientasPctOverride < 0 ? "(hereda proyecto)" : $"{herramientasPctOverride:N2}%";

    // Datos del material vinculado (proveedor, vigencia, fuente)
    public string ProveedorNombre => linkedMaterial?.Proveedor ?? "—";
    public string FechaPrecio => linkedMaterial != null ? linkedMaterial.FechaPrecio.ToString("dd/MM/yyyy") : "—";
    public string FuentePrecio => string.IsNullOrWhiteSpace(linkedMaterial?.OrigenPrecio) ? "—" : linkedMaterial.OrigenPrecio;
    public bool PrecioVencido => linkedMaterial != null && session.ProjectInfo.IsPrecioVencido(linkedMaterial.FechaPrecio);

    public List<string> Alertas
    {
        get => alertas;
        private set => SetProperty(ref alertas, value);
    }

    public bool HasAlertas => alertas.Count > 0;

    // Zonas
    public string[] QtyExpressions { get; private set; } = Array.Empty<string>();
    public string[] QtyHeaders { get; private set; } = Array.Empty<string>();
    public ObservableCollection<ZoneDistributionItem> ZoneDistribution { get; } = new();

    // Controla el mensaje de ayuda de la pestaña Zonas: si el proyecto no tiene zonas
    // nombradas todavía, la lista igual se llena con 15 filas vacías (para poder cubicar
    // por sector sin forzar a configurar zonas antes) — sin este flag esas filas se ven
    // como un menú vacío sin explicación.
    private bool hasZonasConfiguradas;
    public bool HasZonasConfiguradas
    {
        get => hasZonasConfiguradas;
        private set => SetProperty(ref hasZonasConfiguradas, value);
    }

    public void LoadRow(CubicacionRowVm? row)
    {
        currentRow = row;
        if (row == null)
        {
            ClearAll();
            return;
        }

        SetIfChanged(ref zona, row.Area, nameof(Zona));
        SetIfChanged(ref redondeoModo, row.Source.RedondeoModo ?? "Ninguno", nameof(RedondeoModo));
        SetIfChanged(ref redondeoMultiplo, row.Source.RedondeoMultiplo, nameof(RedondeoMultiplo));
        SetIfChanged(ref mermaPctOverride, row.Source.MermaPctOverride, nameof(MermaPctOverride), nameof(MermaPctOverrideDisplay));
        SetIfChanged(ref herramientasPctOverride, row.Source.HerramientasPctOverride, nameof(HerramientasPctOverride), nameof(HerramientasPctOverrideDisplay));

        QtyExpressions = row.Source.QtyExpressions ?? Array.Empty<string>();
        QtyHeaders = GetHeaders(row.ItemId);

        var zonasActivas = session.ZonasProyecto
            .Where(z => z.Activa && !string.IsNullOrWhiteSpace(z.Nombre))
            .OrderBy(z => z.Orden)
            .Select(z => z.Nombre.Trim())
            .ToList();

        // 1. Actualizar los 300 ítems en la lista maestra
        for (int i = 0; i < 300; i++)
        {
            string expr = i < QtyExpressions.Length ? QtyExpressions[i] : "";
            string headerName;
            if (i < QtyHeaders.Length && !string.IsNullOrWhiteSpace(QtyHeaders[i]) && !QtyHeaders[i].StartsWith("Zona ", StringComparison.OrdinalIgnoreCase))
                headerName = QtyHeaders[i].Trim();
            else if (i < zonasActivas.Count)
                headerName = zonasActivas[i];
            else
                headerName = "";
            allZonesMaster[i].Update(headerName, expr);
        }

        HasZonasConfiguradas = zonasActivas.Count > 0;

        // 2. Filtrar dinámicamente qué mostrar en el panel derecho para evitar scroll innecesario
        ZoneDistribution.Clear();
        if (zonasActivas.Count > 0)
        {
            // Caso con zonas de proyecto: Mostrar zonas activas + cualquier zona inactiva que tenga contenido actual
            for (int i = 0; i < 300; i++)
            {
                bool esZonaActiva = i < zonasActivas.Count;
                bool tieneContenido = i < QtyExpressions.Length && !string.IsNullOrWhiteSpace(QtyExpressions[i]);

                if (esZonaActiva || tieneContenido)
                {
                    ZoneDistribution.Add(allZonesMaster[i]);
                }
            }
        }
        else
        {
            // Caso sin zonas configuradas: Mostrar las primeras 15 zonas vacías por defecto,
            // pero si hay contenido guardado más allá de las 15, mostrar también esos campos.
            for (int i = 0; i < 300; i++)
            {
                bool esRangoPorDefecto = i < 15;
                bool tieneContenido = i < QtyExpressions.Length && !string.IsNullOrWhiteSpace(QtyExpressions[i]);

                if (esRangoPorDefecto || tieneContenido)
                {
                    ZoneDistribution.Add(allZonesMaster[i]);
                }
            }
        }

        SetIfChanged(ref comentario, LoadComment(row.Source), nameof(Comentario));
        SetIfChanged(ref linkedMaterial, FindMaterial(row.Codigo), nameof(ProveedorNombre), nameof(FechaPrecio), nameof(FuentePrecio), nameof(PrecioVencido));

        BuildAlertas(row);
        Mediciones.LoadForRow(row);

        // Estas dependen directamente de la fila seleccionada, siempre cambian entre filas.
        OnPropertyChanged(nameof(CurrentRow));
        OnPropertyChanged(nameof(HasCurrentRow));
        OnPropertyChanged(nameof(CurrentRowTitle));
        OnPropertyChanged(nameof(CurrentRowSubtitle));
        OnPropertyChanged(nameof(CurrentRowStatus));
        OnPropertyChanged(nameof(CurrentRowTotals));
        OnPropertyChanged(nameof(Alertas));
        OnPropertyChanged(nameof(HasAlertas));
        OnPropertyChanged(nameof(QtyExpressions));
        OnPropertyChanged(nameof(QtyHeaders));
    }

    private void ClearAll()
    {
        currentRow = null;
        SetIfChanged(ref zona, "General", nameof(Zona));
        SetIfChanged(ref redondeoModo, "Ninguno", nameof(RedondeoModo));
        SetIfChanged(ref redondeoMultiplo, 0m, nameof(RedondeoMultiplo));
        SetIfChanged(ref mermaPctOverride, -1m, nameof(MermaPctOverride), nameof(MermaPctOverrideDisplay));
        SetIfChanged(ref herramientasPctOverride, -1m, nameof(HerramientasPctOverride), nameof(HerramientasPctOverrideDisplay));
        SetIfChanged(ref comentario, "", nameof(Comentario));
        SetIfChanged(ref linkedMaterial, null, nameof(ProveedorNombre), nameof(FechaPrecio), nameof(PrecioVencido));
        QtyExpressions = Array.Empty<string>();
        QtyHeaders = Array.Empty<string>();

        // Limpiar lista maestra y ajustar vista rápida a los primeros 15 vacíos
        for (int i = 0; i < 300; i++)
        {
            allZonesMaster[i].Update("", string.Empty);
        }
        HasZonasConfiguradas = false;
        ZoneDistribution.Clear();
        for (int i = 0; i < 15; i++)
        {
            ZoneDistribution.Add(allZonesMaster[i]);
        }

        alertas = new List<string>();
        Mediciones.LoadForRow(null);

        OnPropertyChanged(nameof(CurrentRow));
        OnPropertyChanged(nameof(HasCurrentRow));
        OnPropertyChanged(nameof(CurrentRowTitle));
        OnPropertyChanged(nameof(CurrentRowSubtitle));
        OnPropertyChanged(nameof(CurrentRowStatus));
        OnPropertyChanged(nameof(CurrentRowTotals));
        OnPropertyChanged(nameof(Alertas));
        OnPropertyChanged(nameof(HasAlertas));
        OnPropertyChanged(nameof(QtyExpressions));
        OnPropertyChanged(nameof(QtyHeaders));
    }

    private void SetIfChanged<T>(ref T field, T value, params string[] propertyNames)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        foreach (var name in propertyNames)
            OnPropertyChanged(name);
    }

    internal void UpdateZoneExpression(int index, string value)
    {
        if (currentRow == null) return;
        parent.SetSectorExpressionDirect(currentRow, index, value);
    }

    private string[] GetHeaders(string itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId) && session.QtyHeadersByItem.TryGetValue(itemId, out var stored))
            return stored ?? Array.Empty<string>();
        return Array.Empty<string>();
    }

    private string LoadComment(CubicacionRow row)
    {
        var comment = session.CubicacionCellComments
            .FirstOrDefault(c => string.Equals(c.CubicacionRowId, row.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.ColumnKey, "row", StringComparison.OrdinalIgnoreCase));
        return comment?.Comment ?? "";
    }

    private void SaveComment(CubicacionRow row, string text)
    {
        var existing = session.CubicacionCellComments
            .FirstOrDefault(c => string.Equals(c.CubicacionRowId, row.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.ColumnKey, "row", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(text))
        {
            if (existing != null) session.CubicacionCellComments.Remove(existing);
            return;
        }

        var now = AppClock.Now;
        if (existing != null)
        {
            existing.Comment = text;
            existing.UpdatedAt = now;
        }
        else
        {
            session.CubicacionCellComments.Add(new CubicacionCellCommentRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = row.ItemId,
                VisualRow = GetVisualIndex(row),
                CubicacionRowId = row.Id,
                ColumnKey = "row",
                Comment = text,
                Author = session.ProjectInfo.UsuarioNombre ?? "",
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private int GetVisualIndex(CubicacionRow row)
    {
        var rows = session.Cubicacion
            .Where(r => string.Equals(r.ItemId, row.ItemId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return rows.IndexOf(row);
    }

    private MaterialRow? FindMaterial(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        return session.Materiales.FirstOrDefault(m =>
            string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
    }

    private void BuildAlertas(CubicacionRowVm row)
    {
        var list = new List<string>();

        if (string.IsNullOrWhiteSpace(row.Codigo))
            list.Add("Sin código de material asignado.");

        if (row.Cantidad == 0m && !row.HasFormula)
            list.Add("Cantidad es cero.");

        if (row.MatClp == 0m && row.MoClp == 0m && row.MatUf == 0m && row.MoUf == 0m)
            list.Add("Precio unitario es cero.");

        if (linkedMaterial == null && !string.IsNullOrWhiteSpace(row.Codigo))
            list.Add("Código no encontrado en el catálogo de materiales.");

        if (linkedMaterial != null && session.ProjectInfo.IsPrecioVencido(linkedMaterial.FechaPrecio))
            list.Add($"Precio con más de {session.ProjectInfo.EffectivePriceExpiryCriticalDays} días de antigüedad ({linkedMaterial.FechaPrecio:dd/MM/yyyy}).");

        alertas = list;
    }
}



public sealed class ZoneDistributionItem : ViewModelBase
{
    private readonly CubicacionRightPanelVm owner;
    private string zoneName = "";
    private string expression = "";

    public ZoneDistributionItem(CubicacionRightPanelVm owner, int index, string zoneName, string expression)
    {
        this.owner = owner;
        Index = index;
        this.zoneName = zoneName;
        this.expression = expression;
    }

    public int Index { get; }

    public string ZoneName
    {
        get => zoneName;
        private set
        {
            if (SetProperty(ref zoneName, value))
                OnPropertyChanged(nameof(ZoneDisplayName));
        }
    }

    // Fallback visible mientras el usuario no haya nombrado la zona en "Zonas…" —
    // sin esto la fila se ve como una caja vacía sin ninguna etiqueta.
    public string ZoneDisplayName => string.IsNullOrWhiteSpace(zoneName) ? $"Zona {Index}" : zoneName;

    public string Expression
    {
        get => expression;
        set
        {
            if (SetProperty(ref expression, value))
            {
                owner.UpdateZoneExpression(Index - 1, value);
            }
        }
    }

    public void Update(string newZoneName, string newExpression)
    {
        if (zoneName != newZoneName)
        {
            zoneName = newZoneName;
            OnPropertyChanged(nameof(ZoneName));
            OnPropertyChanged(nameof(ZoneDisplayName));
        }
        if (expression != newExpression)
        {
            expression = newExpression;
            OnPropertyChanged(nameof(Expression));
        }
    }
}
