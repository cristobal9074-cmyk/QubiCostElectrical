using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PartidaListItemVm : ViewModelBase
{
    private bool isActiva;
    private bool isTagged;
    private int recursoCount;
    private bool excluidoCubicacion;
    private string motivoNoCubicar;
    private readonly ItemizadoRow source;

    public PartidaListItemVm(ItemizadoRow row, int recursoCount = 0, bool hasIssues = false, bool isPureSectionTitle = false)
    {
        source = row ?? throw new ArgumentNullException(nameof(row));
        Id = row.Id;
        codigo = row.Item ?? "";
        descripcion = row.Descripcion ?? "";
        EsPartida = !isPureSectionTitle;
        excluidoCubicacion = row.ExcluidoCubicacion;
        motivoNoCubicar = row.MotivoNoCubicar ?? "";
        RecursoCount = recursoCount;
        HasIssues = hasIssues;
    }

    private string codigo;
    private string descripcion;

    public string Id { get; }
    public string Codigo
    {
        get => codigo;
        set
        {
            if (SetProperty(ref codigo, value ?? ""))
            {
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(ExclusionTooltip));
            }
        }
    }

    public string Descripcion
    {
        get => descripcion;
        set
        {
            if (SetProperty(ref descripcion, value ?? ""))
            {
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(ExclusionTooltip));
            }
        }
    }
    public bool IsTagged
    {
        get => isTagged;
        set => SetProperty(ref isTagged, value);
    }
    public string MotivoNoCubicar
    {
        get => motivoNoCubicar;
        set
        {
            if (SetProperty(ref motivoNoCubicar, value ?? ""))
            {
                OnPropertyChanged(nameof(ExclusionTooltip));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }
    public string Unidad
    {
        get => source.Unidad ?? "";
        set
        {
            string clean = value ?? "";
            if (string.Equals(source.Unidad ?? "", clean, StringComparison.Ordinal))
                return;
            source.Unidad = clean;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SinValorizacionGlobal));
            OnPropertyChanged(nameof(HasMissingGlobalUnidad));
            OnPropertyChanged(nameof(StatusBadgeText));
            OnPropertyChanged(nameof(ExclusionTooltip));
        }
    }
    public bool EsPartida { get; }
    public bool ExcluidoCubicacion
    {
        get => excluidoCubicacion;
        set
        {
            if (SetProperty(ref excluidoCubicacion, value))
            {
                OnPropertyChanged(nameof(ExclusionTooltip));
                OnPropertyChanged(nameof(SinValorizacionGlobal));
                OnPropertyChanged(nameof(HasMissingGlobalUnidad));
                OnPropertyChanged(nameof(HasMissingGlobalCantidad));
                OnPropertyChanged(nameof(StatusBadgeText));
            }
        }
    }
    public decimal Cantidad
    {
        get => source.Cantidad;
        set
        {
            if (source.Cantidad == value)
                return;
            source.Cantidad = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SinValorizacionGlobal));
            OnPropertyChanged(nameof(HasMissingGlobalCantidad));
            OnPropertyChanged(nameof(StatusBadgeText));
            OnPropertyChanged(nameof(ExclusionTooltip));
        }
    }

    private string cantidadFormula = "";
    public string CantidadFormula
    {
        get => cantidadFormula;
        set
        {
            if (SetProperty(ref cantidadFormula, value ?? ""))
                OnPropertyChanged(nameof(CantidadToolTip));
        }
    }

    public string CantidadToolTip => !string.IsNullOrWhiteSpace(cantidadFormula)
        ? $"Fórmula: {cantidadFormula}\nResultado: {(Cantidad % 1 == 0 ? Cantidad.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : Cantidad.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture))}"
        : "Cantidad oficial de la partida. No modifica las cantidades de cada material.";

    public int RecursoCount
    {
        get => recursoCount;
        set
        {
            if (SetProperty(ref recursoCount, value))
            {
                OnPropertyChanged(nameof(TieneCubicacion));
                OnPropertyChanged(nameof(SinValorizacionGlobal));
                OnPropertyChanged(nameof(HasMissingGlobalUnidad));
                OnPropertyChanged(nameof(HasMissingGlobalCantidad));
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(ExclusionTooltip));
            }
        }
    }

    public bool TieneCubicacion => recursoCount > 0;

    /// <summary>
    /// Alerta: la partida tiene materiales cubicados pero le falta la unidad o cantidad global en el encabezado.
    /// </summary>
    public bool SinValorizacionGlobal => EsPartida && TieneCubicacion && !ExcluidoCubicacion && (Cantidad <= 0m || string.IsNullOrWhiteSpace(Unidad));
    public bool HasMissingGlobalUnidad => EsPartida && TieneCubicacion && !ExcluidoCubicacion && string.IsNullOrWhiteSpace(Unidad);
    public bool HasMissingGlobalCantidad => EsPartida && TieneCubicacion && !ExcluidoCubicacion && Cantidad <= 0m;

    // Código no encontrado en el catálogo, o precio en cero, en al menos un recurso de la
    // partida. Antes solo se detectaba abriendo la partida y mirando fila por fila; con
    // proyectos grandes era fácil dejar partidas rotas sin darse cuenta.
    public bool HasIssues { get; }

    public bool IsActiva
    {
        get => isActiva;
        set => SetProperty(ref isActiva, value);
    }

    public string DisplayLabel => EsPartida
        ? (string.IsNullOrWhiteSpace(Codigo) ? Descripcion : $"{Codigo}  {Descripcion}")
        : $"  {Descripcion}";

    /// <summary>Muestra alerta cuando falta valorización global, observación si hay texto o estado de cubicación.</summary>
    public string StatusBadgeText
    {
        get
        {
            var nota = MotivoNoCubicar.Trim();
            if (!string.IsNullOrEmpty(nota))
                return nota;
            if (SinValorizacionGlobal)
            {
                if (HasMissingGlobalUnidad && HasMissingGlobalCantidad)
                    return "Sin unidad ni cant. global";
                if (HasMissingGlobalCantidad)
                    return "Sin cantidad global";
                return "Sin unidad global";
            }
            if (HasIssues)
                return "Con observaciones";
            return TieneCubicacion ? "Cubicada" : "Sin cubicación";
        }
    }

    public string ExclusionTooltip
    {
        get
        {
            var motivo = MotivoNoCubicar.Trim();
            if (!string.IsNullOrEmpty(motivo))
                return $"{DisplayLabel}\n{motivo}";

            if (SinValorizacionGlobal)
            {
                if (HasMissingGlobalUnidad && HasMissingGlobalCantidad)
                    return $"{DisplayLabel}\n⚠️ Partida cubicada sin unidad ni cantidad global (arriba).";
                if (HasMissingGlobalCantidad)
                    return $"{DisplayLabel}\n⚠️ Partida cubicada con cantidad global en cero (arriba).";
                return $"{DisplayLabel}\n⚠️ Partida cubicada sin unidad de medida global (arriba).";
            }

            return DisplayLabel;
        }
    }
}
