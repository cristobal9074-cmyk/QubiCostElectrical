using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Nombres de columna estilo Excel (A, B... Z, AA, AB...) compartidos entre el modelo (para
/// resolver variables de fórmulas) y el code-behind del diálogo (para los encabezados) — ambos
/// deben usar exactamente la misma letra para la misma columna, o las fórmulas del usuario
/// (que escriben la letra que ven en el encabezado) apuntarían a la columna equivocada.
/// </summary>
internal static class HojaCubicacionColumns
{
    /// <summary>Letra para una columna de datos (0-based); las columnas de datos parten en "C"
    /// porque A = Descripción y B = Suma están reservadas.</summary>
    public static string DataColumnLetter(int zeroBasedDataIndex) => ExcelColumnName(zeroBasedDataIndex + 2);

    private static string ExcelColumnName(int zeroBasedIndex)
    {
        int dividend = zeroBasedIndex + 1;
        string name = "";
        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            name = (char)('A' + modulo) + name;
            dividend = (dividend - modulo - 1) / 26;
        }
        return name;
    }
}

/// <summary>
/// Fila de datos dentro de un ítem de la Hoja de Cubicación: una descripción (columna A) y una
/// serie de valores por columna (C en adelante). Cada valor puede ser un número simple o una
/// fórmula (p. ej. "=C+D*2") que referencia otras columnas de la MISMA fila por su letra —
/// igual que Excel, pero acotado a la fila (sin referencias entre filas ni entre ítems).
/// Las celdas vacías se muestran en blanco (no "0"), para no ensuciar la navegación con teclado
/// entre las 300 columnas disponibles.
/// </summary>
public sealed class HojaCubicacionFilaVm : INotifyPropertyChanged
{
    private string descripcion;
    private decimal suma;
    private string sumaDisplay = "";

    public HojaCubicacionFilaVm(string descripcion, int columnCount, bool isLabelRow = false)
    {
        this.descripcion = descripcion;
        IsLabelRow = isLabelRow;
        Valores = new ObservableCollection<string>(Enumerable.Repeat("", columnCount));
        ValoresDisplay = new ObservableCollection<string>(Enumerable.Repeat("", columnCount));
        Valores.CollectionChanged += OnValoresChanged;
    }

    /// <summary>Ítem al que pertenece esta fila; permite ubicar el item dueño desde un handler de fila.</summary>
    internal HojaCubicacionItemVm? Owner { get; set; }

    /// <summary>
    /// Fila especial, siempre la primera del ítem, para NOMBRAR cada columna (ej. "Depto 1",
    /// "Piso 1") antes de cargar sus datos numéricos debajo. Es una fila REAL de la grilla (no un
    /// control aparte metido en el encabezado) — por eso tiene automáticamente las mismas
    /// funciones que cualquier otra fila: selección, navegación con teclado, copiar/pegar,
    /// Ctrl+D/R para rellenar correlativos (1, 2, 3...20). No participa de fórmulas ni de la
    /// suma (su contenido es texto libre), y no se puede eliminar.
    /// </summary>
    public bool IsLabelRow { get; }

    public string Descripcion
    {
        get => descripcion;
        set { descripcion = value ?? ""; Raise(nameof(Descripcion)); Raise(nameof(DescripcionDisplay)); }
    }

    /// <summary>Igual que Descripcion, salvo en la fila de nombres de columna vacía: ahí muestra
    /// un texto guía en vez de dejarla en blanco, para que se note a simple vista que esa fila es
    /// solo para nombrar columnas (ej. "Depto 1") y no una fila de datos a cubicar.</summary>
    public string DescripcionDisplay => IsLabelRow && descripcion.Length == 0
        ? "Nombra las columnas aquí →"
        : descripcion;

    /// <summary>Texto crudo por columna (lo que el usuario escribió: número o fórmula). Editable.</summary>
    public ObservableCollection<string> Valores { get; }

    /// <summary>Texto a mostrar por columna cuando no se está editando: el resultado calculado,
    /// o vacío si la celda no tiene datos — nunca "0" de relleno.</summary>
    public ObservableCollection<string> ValoresDisplay { get; }

    public decimal Suma
    {
        get => suma;
        private set { suma = value; Raise(nameof(Suma)); }
    }

    public string SumaDisplay
    {
        get => sumaDisplay;
        private set { sumaDisplay = value; Raise(nameof(SumaDisplay)); }
    }

    /// <summary>Agrega una columna nueva a esta fila (Valores Y ValoresDisplay a la vez, para que
    /// no queden desalineadas — si solo crece Valores, la celda nueva queda sin poder mostrar
    /// nada porque ValoresDisplay[i] estaría fuera de rango).</summary>
    internal void AddColumnSlot()
    {
        Valores.Add("");
        ValoresDisplay.Add("");
    }

    private void OnValoresChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDisplay();

    /// <summary>Recalcula ValoresDisplay y Suma. Se llama cuando cambia cualquier celda de la fila,
    /// porque una fórmula puede depender de una celda vecina recién editada.</summary>
    internal void RefreshDisplay()
    {
        // La fila de nombres de columna es texto libre (ej. "Depto 1"): se muestra tal cual, sin
        // intentar evaluarla como número o fórmula, y no aporta a ninguna suma.
        if (IsLabelRow)
        {
            for (int i = 0; i < Valores.Count && i < ValoresDisplay.Count; i++)
                ValoresDisplay[i] = Valores[i] ?? "";
            Suma = 0m;
            SumaDisplay = "";
            return;
        }

        // Variables disponibles para las fórmulas: solo columnas con un número literal (no otra
        // fórmula) — evita tener que resolver dependencias encadenadas entre celdas.
        var literales = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase);
        for (int j = 0; j < Valores.Count; j++)
        {
            if (TryParseLiteral(Valores[j], out decimal literal))
                literales[HojaCubicacionColumns.DataColumnLetter(j)] = literal;
        }

        decimal total = 0m;
        for (int i = 0; i < Valores.Count; i++)
        {
            string raw = (Valores[i] ?? "").Trim();
            if (raw.Length == 0)
            {
                if (i < ValoresDisplay.Count) ValoresDisplay[i] = "";
                continue;
            }

            var result = QuantityFormulaService.Evaluate(raw, literales);
            decimal cellValue = result.Success ? result.Value : 0m;
            total += cellValue;

            if (i < ValoresDisplay.Count)
                ValoresDisplay[i] = result.Success ? FormatDecimal(cellValue) : "¡ERROR!";
        }

        Suma = total;
        SumaDisplay = total == 0m ? "" : FormatDecimal(total);
    }

    private static bool TryParseLiteral(string? raw, out decimal value)
    {
        value = 0m;
        string text = (raw ?? "").Trim();
        if (text.Length == 0) return false;
        return decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static string FormatDecimal(decimal value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Un ítem (partida) a cubicar dentro de la Hoja de Cubicación compartida: agrupa varias filas
/// de datos bajo un título y código propios, con su propio subtotal.
/// </summary>
public sealed class HojaCubicacionItemVm : INotifyPropertyChanged
{
    private string codigo;
    private string titulo;
    private string unidad;
    private bool isCollapsed;

    public HojaCubicacionItemVm(string codigo, string titulo, string unidad, int columnCount)
        : this(codigo, titulo, unidad, columnCount, createDefaultRows: true)
    {
    }

    private HojaCubicacionItemVm(string codigo, string titulo, string unidad, int columnCount, bool createDefaultRows)
    {
        this.codigo = codigo;
        this.titulo = titulo;
        this.unidad = unidad;
        Filas = new ObservableCollection<HojaCubicacionFilaVm>();

        if (!createDefaultRows)
            return;

        // Fila fija de nombres de columna (ej. "Depto 1", "Piso 1"), siempre primera, para
        // nombrar cada columna antes de cargar sus datos numéricos debajo. Es una fila real de
        // la grilla, así que hereda gratis toda la funcionalidad de cualquier otra celda.
        var etiquetas = new HojaCubicacionFilaVm("", columnCount, isLabelRow: true) { Owner = this };
        Filas.Add(etiquetas);

        AddFila(columnCount, "");
    }

    /// <summary>Reconstruye un ítem a partir de lo guardado en el proyecto (ver
    /// <see cref="ToSaved"/>), respetando exactamente las filas, columnas y valores que tenía —
    /// a diferencia del constructor público, que arma un ítem nuevo en blanco.</summary>
    public static HojaCubicacionItemVm FromSaved(HojaCubicacionItem saved)
    {
        int columnCount = 0;
        foreach (var f in saved.Filas)
            columnCount = System.Math.Max(columnCount, f.Valores?.Count ?? 0);
        if (columnCount == 0) columnCount = 3;

        var item = new HojaCubicacionItemVm(saved.Codigo ?? "", saved.Titulo ?? "", saved.Unidad ?? "ml", columnCount, createDefaultRows: false);

        if (saved.Filas.Count == 0)
        {
            var etiquetas = new HojaCubicacionFilaVm("", columnCount, isLabelRow: true) { Owner = item };
            item.Filas.Add(etiquetas);
            item.AddFila(columnCount, "");
        }
        else
        {
            foreach (var filaSaved in saved.Filas)
                item.AddFilaFromSaved(filaSaved, columnCount);
        }

        return item;
    }

    /// <summary>Vuelca este ítem a su forma persistible (ver <see cref="FromSaved"/>).</summary>
    public HojaCubicacionItem ToSaved() => new()
    {
        Codigo = Codigo,
        Titulo = Titulo,
        Unidad = Unidad,
        Filas = Filas.Select(f => new HojaCubicacionFila
        {
            Descripcion = f.Descripcion,
            IsLabelRow = f.IsLabelRow,
            Valores = f.Valores.ToList()
        }).ToList()
    };

    private HojaCubicacionFilaVm AddFilaFromSaved(HojaCubicacionFila saved, int columnCount)
    {
        var fila = new HojaCubicacionFilaVm(saved.Descripcion ?? "", columnCount, saved.IsLabelRow) { Owner = this };
        var savedValores = saved.Valores ?? new List<string>();
        for (int i = 0; i < columnCount && i < savedValores.Count; i++)
            fila.Valores[i] = savedValores[i] ?? "";
        fila.PropertyChanged += OnFilaPropertyChanged;
        Filas.Add(fila);
        return fila;
    }

    public string Codigo
    {
        get => codigo;
        set { codigo = value ?? ""; Raise(nameof(Codigo)); }
    }

    public string Titulo
    {
        get => titulo;
        set { titulo = value ?? ""; Raise(nameof(Titulo)); }
    }

    public string Unidad
    {
        get => unidad;
        set { unidad = value ?? ""; Raise(nameof(Unidad)); }
    }

    public bool IsCollapsed
    {
        get => isCollapsed;
        set
        {
            isCollapsed = value;
            Raise(nameof(IsCollapsed));
            Raise(nameof(IsExpanded));
            Raise(nameof(CollapseIcon));
        }
    }

    /// <summary>Inverso de IsCollapsed, para bindear visibilidad del cuerpo con BoolToVisibility.</summary>
    public bool IsExpanded => !isCollapsed;

    public string CollapseIcon => isCollapsed ? "▸" : "▾";

    public ObservableCollection<HojaCubicacionFilaVm> Filas { get; }

    /// <summary>Cantidad de columnas de datos (C en adelante) que tiene este ítem actualmente.</summary>
    public int ColumnCount => Filas.Count > 0 ? Filas[0].Valores.Count : 0;

    public decimal Subtotal => Filas.Sum(f => f.Suma);

    public HojaCubicacionFilaVm AddFila(int? columnCount = null, string descripcion = "")
    {
        var fila = new HojaCubicacionFilaVm(descripcion, columnCount ?? ColumnCount) { Owner = this };
        fila.PropertyChanged += OnFilaPropertyChanged;
        Filas.Add(fila);
        Raise(nameof(Subtotal));
        return fila;
    }

    public void RemoveFila(HojaCubicacionFilaVm fila)
    {
        if (fila.IsLabelRow) return;
        if (Filas.Count <= 1) return;
        fila.PropertyChanged -= OnFilaPropertyChanged;
        Filas.Remove(fila);
        Raise(nameof(Subtotal));
    }

    public void AddColumn()
    {
        foreach (var fila in Filas)
        {
            fila.AddColumnSlot();
        }
        Raise(nameof(ColumnCount));
    }

    private void OnFilaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HojaCubicacionFilaVm.Suma))
            Raise(nameof(Subtotal));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
