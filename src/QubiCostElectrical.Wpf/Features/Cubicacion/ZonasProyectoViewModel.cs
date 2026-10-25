using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Modelo de vista para la gestión ejecutiva de zonas del proyecto (edificio, torres, niveles).
/// </summary>
public sealed class ZonasProyectoViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private ZonaEditRowVm? _selectedRow;

    public ObservableCollection<ZonaEditRowVm> Rows { get; } = new();
    public ICollectionView FilteredRows { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ZonasProyectoViewModel()
    {
        FilteredRows = CollectionViewSource.GetDefaultView(Rows);
        FilteredRows.Filter = FilterRow;
        Rows.CollectionChanged += (_, _) => RefreshStatsAndNumbers();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            FilteredRows.Refresh();
        }
    }

    public ZonaEditRowVm? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (_selectedRow == value) return;
            _selectedRow = value;
            OnPropertyChanged(nameof(SelectedRow));
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
            OnPropertyChanged(nameof(CanDelete));
        }
    }

    public int TotalZonasCount => Rows.Count;
    public int ActivasCount => Rows.Count(r => r.Activa);
    public int InactivasCount => Rows.Count(r => !r.Activa);
    public bool HasNoZonas => Rows.Count == 0;

    public bool CanMoveUp => SelectedRow != null && Rows.IndexOf(SelectedRow) > 0;
    public bool CanMoveDown => SelectedRow != null && Rows.IndexOf(SelectedRow) >= 0 && Rows.IndexOf(SelectedRow) < Rows.Count - 1;
    public bool CanDelete => SelectedRow != null;

    public void CargarZonas(IEnumerable<ZonaProyecto> zonas)
    {
        Rows.Clear();
        foreach (var zona in zonas.OrderBy(z => z.Orden))
        {
            var row = new ZonaEditRowVm(zona.Id, zona.Nombre, zona.Activa);
            row.PropertyChanged += OnRowPropertyChanged;
            Rows.Add(row);
        }
        RefreshStatsAndNumbers();
    }

    public ZonaEditRowVm AgregarZona(string nombre = "")
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = SugerirSiguienteNombreZona();
        }

        var row = new ZonaEditRowVm(Guid.NewGuid().ToString("N"), nombre, true);
        row.PropertyChanged += OnRowPropertyChanged;
        Rows.Add(row);
        SelectedRow = row;
        return row;
    }

    /// <summary>
    /// Deduce de forma inteligente el siguiente nombre secuencial según la última zona existente
    /// (ej. Piso 1 → Piso 2, Subterráneo 1 → Subterráneo 2, Subterráneo -1 → Subterráneo -2).
    /// </summary>
    public string SugerirSiguienteNombreZona()
    {
        if (Rows.Count == 0)
            return "Piso 1";

        var ultima = Rows[^1].Nombre.Trim();
        if (string.IsNullOrWhiteSpace(ultima))
            return $"Piso {Rows.Count + 1}";

        // Patrón Subterráneo con número negativo: ej. "Subterráneo -1" → "Subterráneo -2"
        var matchSubNeg = System.Text.RegularExpressions.Regex.Match(ultima, @"^(.*?)(-\d+)$");
        if (matchSubNeg.Success && int.TryParse(matchSubNeg.Groups[2].Value, out int numNeg))
        {
            return $"{matchSubNeg.Groups[1].Value}{numNeg - 1}";
        }

        // Patrón general: termina en número (ej. "Piso 1", "Piso 12", "Nivel 3", "Torre A - Piso 4")
        var matchNum = System.Text.RegularExpressions.Regex.Match(ultima, @"^(.*?)(\d+)$");
        if (matchNum.Success && int.TryParse(matchNum.Groups[2].Value, out int num))
        {
            return $"{matchNum.Groups[1].Value}{num + 1}";
        }

        if (ultima.Equals("Subterráneo", StringComparison.OrdinalIgnoreCase) ||
            ultima.Equals("Subterraneo", StringComparison.OrdinalIgnoreCase))
            return "Subterráneo -1";

        return $"Piso {Rows.Count + 1}";
    }

    /// <summary>
    /// Genera rápidamente una serie de pisos o niveles (ej. Pisos 1 al 20, o Subterráneos 1 al 3).
    /// </summary>
    public void GenerarRangoPisos(string prefijo, int desde, int hasta, bool reemplazarExistentes)
    {
        if (reemplazarExistentes)
        {
            Rows.Clear();
        }

        if (desde <= hasta)
        {
            for (int i = desde; i <= hasta; i++)
            {
                AgregarZona($"{prefijo}{i}");
            }
        }
        else
        {
            for (int i = desde; i >= hasta; i--)
            {
                AgregarZona($"{prefijo}{i}");
            }
        }
    }

    public void QuitarZona(ZonaEditRowVm? row = null)
    {
        var target = row ?? SelectedRow;
        if (target == null) return;

        int currentIndex = Rows.IndexOf(target);
        target.PropertyChanged -= OnRowPropertyChanged;
        Rows.Remove(target);

        if (Rows.Count > 0)
        {
            int nextIndex = Math.Min(currentIndex, Rows.Count - 1);
            SelectedRow = Rows[nextIndex];
        }
        else
        {
            SelectedRow = null;
        }
    }

    public void MoveZona(ZonaEditRowVm row, int delta)
    {
        int index = Rows.IndexOf(row);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= Rows.Count)
            return;

        Rows.Move(index, target);
        RefreshStatsAndNumbers();
        SelectedRow = row;
    }

    public void MoveSelected(int delta)
    {
        if (SelectedRow != null)
            MoveZona(SelectedRow, delta);
    }

    public void SetActivasTodas(bool activar)
    {
        foreach (var row in Rows)
            row.Activa = activar;
        RefreshStatsAndNumbers();
    }

    public void RefreshStatsAndNumbers()
    {
        for (int i = 0; i < Rows.Count; i++)
            Rows[i].Numero = i + 1;

        OnPropertyChanged(nameof(TotalZonasCount));
        OnPropertyChanged(nameof(ActivasCount));
        OnPropertyChanged(nameof(InactivasCount));
        OnPropertyChanged(nameof(HasNoZonas));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        OnPropertyChanged(nameof(CanDelete));
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ZonaEditRowVm.Activa))
        {
            OnPropertyChanged(nameof(ActivasCount));
            OnPropertyChanged(nameof(InactivasCount));
        }
        else if (e.PropertyName == nameof(ZonaEditRowVm.Nombre))
        {
            FilteredRows.Refresh();
        }
    }

    private bool FilterRow(object item)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        if (item is not ZonaEditRowVm row)
            return false;

        string query = _searchText.Trim();
        if (row.Numero.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (row.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (row.TipoDisplay.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public List<ZonaProyecto> ObtenerZonasResultantes()
    {
        var result = new List<ZonaProyecto>();
        for (int i = 0; i < Rows.Count; i++)
        {
            result.Add(new ZonaProyecto
            {
                Id = Rows[i].Id,
                Nombre = Rows[i].Nombre.Trim(),
                Orden = i,
                Activa = Rows[i].Activa
            });
        }
        return result;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Fila de edición de zona individual con enriquecimiento visual para software de clase mundial.
/// </summary>
public sealed class ZonaEditRowVm : INotifyPropertyChanged
{
    private int _numero;
    private string _nombre;
    private bool _activa;

    public string Id { get; }

    public ZonaEditRowVm(string id, string nombre, bool activa)
    {
        Id = id;
        _nombre = nombre ?? string.Empty;
        _activa = activa;
    }

    public int Numero
    {
        get => _numero;
        set
        {
            if (_numero == value) return;
            _numero = value;
            Raise(nameof(Numero));
            Raise(nameof(NumeroDisplay));
        }
    }

    public string NumeroDisplay => _numero < 10 ? $"0{_numero}" : _numero.ToString();

    public string Nombre
    {
        get => _nombre;
        set
        {
            string val = value ?? string.Empty;
            if (_nombre == val) return;
            _nombre = val;
            Raise(nameof(Nombre));
            Raise(nameof(TipoDisplay));
            Raise(nameof(TipoBadgeBg));
            Raise(nameof(TipoBadgeBorder));
            Raise(nameof(TipoBadgeFg));
        }
    }

    public bool Activa
    {
        get => _activa;
        set
        {
            if (_activa == value) return;
            _activa = value;
            Raise(nameof(Activa));
            Raise(nameof(EstadoBadgeBg));
            Raise(nameof(EstadoBadgeBorder));
            Raise(nameof(EstadoBadgeFg));
            Raise(nameof(EstadoTexto));
        }
    }

    public void ToggleActiva() => Activa = !Activa;

    public string EstadoTexto => Activa ? "● Activa" : "○ Inactiva";
    public string EstadoBadgeBg => Activa ? "#ECFDF5" : "#F8FAFC";
    public string EstadoBadgeBorder => Activa ? "#A7F3D0" : "#E2E8F0";
    public string EstadoBadgeFg => Activa ? "#059669" : "#94A3B8";

    public string TipoDisplay
    {
        get
        {
            string n = _nombre.Trim();
            if (string.IsNullOrEmpty(n)) return "Sector";
            if (n.Contains("sub", StringComparison.OrdinalIgnoreCase)) return "Subterráneo";
            if (n.Contains("azotea", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("cubierta", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("techo", StringComparison.OrdinalIgnoreCase)) return "Cubierta";
            if (n.Contains("sala", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("tablero", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("shaft", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("bomba", StringComparison.OrdinalIgnoreCase)) return "Técnico";
            if (n.Contains("piso", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("torre", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("nivel", StringComparison.OrdinalIgnoreCase)) return "Nivel";
            return "Sector";
        }
    }

    public string TipoBadgeBg => TipoDisplay switch
    {
        "Subterráneo" => "#F1F5F9",
        "Cubierta" => "#FAF5FF",
        "Técnico" => "#FFFBEB",
        "Nivel" => "#EFF6FF",
        _ => "#F8FAFC"
    };

    public string TipoBadgeBorder => TipoDisplay switch
    {
        "Subterráneo" => "#CBD5E1",
        "Cubierta" => "#E9D5FF",
        "Técnico" => "#FDE68A",
        "Nivel" => "#BFDBFE",
        _ => "#E2E8F0"
    };

    public string TipoBadgeFg => TipoDisplay switch
    {
        "Subterráneo" => "#475569",
        "Cubierta" => "#7E22CE",
        "Técnico" => "#B45309",
        "Nivel" => "#1D4ED8",
        _ => "#64748B"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
