using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Provee acceso por reflexión, parseo numérico flexible, relleno de secuencias y operaciones
/// de portapapeles sobre celdas y filas de DataGrid estilo Excel.
/// </summary>
public static class DataGridCellAccessor
{
    private static readonly ConcurrentDictionary<Type, MethodInfo?> _beginBatchCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> _endBatchCache = new();
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propertyCache = new();

    public static bool TryParseFlexibleDecimal(string? raw, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string clean = raw.Trim();
        clean = clean.Replace("$", "")
                     .Replace("€", "")
                     .Replace("UF", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("CLP", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("%", "")
                     .Trim();

        if (string.IsNullOrWhiteSpace(clean))
            return false;

        int lastDot = clean.LastIndexOf('.');
        int lastComma = clean.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
            clean = lastComma > lastDot ? clean.Replace(".", "").Replace(',', '.') : clean.Replace(",", "");
        else if (lastComma >= 0)
            clean = clean.Replace(',', '.');
        else if (lastDot >= 0 && clean.Count(c => c == '.') > 1)
            clean = clean.Replace(".", "");

        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    public static string GetCellText(object? dataContext, DataGridColumn? column)
    {
        if (dataContext == null || column == null)
            return string.Empty;

        if (!TryGetBoundPropertyName(column, out var propertyName))
            return string.Empty;

        if (TryGetIndexedList(dataContext, propertyName, out var list, out var index))
            return list != null && index >= 0 && index < list.Count ? list[index] ?? "" : "";

        var prop = GetCachedProperty(dataContext.GetType(), propertyName);
        return prop?.GetValue(dataContext)?.ToString() ?? "";
    }

    public static void SetCellText(object? dataContext, DataGridColumn? column, string value)
    {
        if (dataContext == null || column == null)
            return;

        if (!TryGetBoundPropertyName(column, out var propertyName))
            return;

        if (TryGetIndexedList(dataContext, propertyName, out var list, out var index))
        {
            if (list != null && index >= 0 && index < list.Count)
                list[index] = value;
            return;
        }

        var prop = GetCachedProperty(dataContext.GetType(), propertyName);
        if (prop == null || !prop.CanWrite)
            return;

        Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (propType == typeof(string))
        {
            prop.SetValue(dataContext, value);
        }
        else if (propType == typeof(decimal))
        {
            if (TryParseFlexibleDecimal(value, out var d))
                prop.SetValue(dataContext, d);
            else if (string.IsNullOrWhiteSpace(value))
                prop.SetValue(dataContext, prop.PropertyType == typeof(decimal?) ? null : 0m);
        }
        else if (propType == typeof(double))
        {
            if (TryParseFlexibleDecimal(value, out var d))
                prop.SetValue(dataContext, (double)d);
            else if (string.IsNullOrWhiteSpace(value))
                prop.SetValue(dataContext, prop.PropertyType == typeof(double?) ? null : 0.0);
        }
        else if (propType == typeof(int))
        {
            if (TryParseFlexibleDecimal(value, out var d))
                prop.SetValue(dataContext, (int)Math.Round(d));
            else if (string.IsNullOrWhiteSpace(value))
                prop.SetValue(dataContext, prop.PropertyType == typeof(int?) ? null : 0);
        }
        else if (propType == typeof(bool))
        {
            if (bool.TryParse(value, out var b))
                prop.SetValue(dataContext, b);
            else if (value is "1" or "si" or "sí" or "true" or "yes" or "TRUE" or "SI")
                prop.SetValue(dataContext, true);
            else if (value is "0" or "no" or "false" or "FALSE" or "NO")
                prop.SetValue(dataContext, false);
        }
    }

    public static bool TryGetBoundPropertyName(DataGridColumn column, out string propertyName)
    {
        propertyName = "";
        Binding? binding = null;
        if (column is DataGridBoundColumn boundColumn)
            binding = boundColumn.Binding as Binding;
        binding ??= column.ClipboardContentBinding as Binding;
        if (binding?.Path?.Path is not { Length: > 0 } path)
            return false;
        propertyName = path;
        return true;
    }

    public static PropertyInfo? GetCachedProperty(Type type, string name)
        => _propertyCache.GetOrAdd((type, name), key => key.Item1.GetProperty(key.Item2));

    public static bool TryGetIndexedList(object dataContext, string propertyPath, out ObservableCollection<string>? list, out int index)
    {
        list = null;
        index = -1;
        int bracketIndex = propertyPath.IndexOf('[');
        if (bracketIndex <= 0)
            return false;

        string listPropertyName = propertyPath.Substring(0, bracketIndex);
        list = GetCachedProperty(dataContext.GetType(), listPropertyName)?.GetValue(dataContext) as ObservableCollection<string>;
        index = ParseIndexerIndex(propertyPath);
        return true;
    }

    public static int ParseIndexerIndex(string propertyPath)
    {
        var parts = propertyPath.Split('[', ']');
        return parts.Length > 1 && int.TryParse(parts[1], out int index) ? index : -1;
    }

    public static void BeginBatchUpdate(object? item)
    {
        if (item == null) return;
        var method = _beginBatchCache.GetOrAdd(item.GetType(), t => t.GetMethod("BeginBatchUpdate", Type.EmptyTypes));
        method?.Invoke(item, null);
    }

    public static void EndBatchUpdate(object? item)
    {
        if (item == null) return;
        var method = _endBatchCache.GetOrAdd(item.GetType(), t => t.GetMethod("EndBatchUpdate", Type.EmptyTypes));
        method?.Invoke(item, null);
    }

    public static bool FillSelection(DataGrid grid, bool vertical)
    {
        if (grid.SelectedCells.Count is < 2 or > 50)
            return false;
        int totalRows = grid.Items.Count;
        if (totalRows <= 1)
            return false;
        var cells = grid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        if (cells.Count < 2)
            return false;

        var distinctRows = cells.Select(c => c.Item).Distinct().ToList();
        var distinctCols = cells.Select(c => c.Column).Distinct().ToList();

        if (distinctRows.Count >= totalRows || distinctCols.Count > 2 || cells.Count > 50)
            return false;

        bool filledAny = false;
        var indexByItem = new Dictionary<object, int>(totalRows);
        for (int i = 0; i < totalRows; i++)
            if (grid.Items[i] is { } it && !indexByItem.ContainsKey(it)) indexByItem[it] = i;

        var affectedItems = distinctRows;
        foreach (var item in affectedItems)
            BeginBatchUpdate(item);

        try
        {
            if (vertical)
            {
                foreach (var group in cells.GroupBy(c => c.Column))
                {
                    var ordered = group
                        .Select(c => (Item: c.Item, RowIndex: (c.Item != null && indexByItem.TryGetValue(c.Item, out int idx)) ? idx : -1))
                        .Where(t => t.RowIndex >= 0)
                        .OrderBy(t => t.RowIndex)
                        .Select(t => (t.Item, group.Key))
                        .ToList();
                    if (FillAlongSequence(ordered))
                        filledAny = true;
                }
            }
            else
            {
                foreach (var group in cells.GroupBy(c => c.Item))
                {
                    var ordered = group
                        .OrderBy(c => c.Column.DisplayIndex)
                        .Select(c => (group.Key, c.Column))
                        .ToList();
                    if (FillAlongSequence(ordered))
                        filledAny = true;
                }
            }
        }
        finally
        {
            foreach (var item in affectedItems)
                EndBatchUpdate(item);
        }

        return filledAny;
    }

    public static bool FillAlongSequence(IReadOnlyList<(object Item, DataGridColumn Column)> ordered)
    {
        if (ordered.Count < 2) return false;
        string first = GetCellText(ordered[0].Item, ordered[0].Column);
        string second = GetCellText(ordered[1].Item, ordered[1].Column);
        bool firstIsNumber = decimal.TryParse(first.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v1);
        bool secondIsNumber = decimal.TryParse(second.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2);

        if (firstIsNumber && secondIsNumber)
        {
            decimal step = v2 - v1;
            for (int i = 2; i < ordered.Count; i++)
                SetCellText(ordered[i].Item, ordered[i].Column, (v1 + step * i).ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            string seed = !string.IsNullOrWhiteSpace(first) ? first : second;
            for (int i = 1; i < ordered.Count; i++)
                SetCellText(ordered[i].Item, ordered[i].Column, seed);
        }
        return true;
    }

    public static void ClearSelection(DataGrid grid)
    {
        var cells = grid.SelectedCells.Where(c => c.IsValid && c.Column != null && !c.Column.IsReadOnly).ToList();
        if (cells.Count == 0) return;

        foreach (var group in cells.GroupBy(c => c.Item))
        {
            var rowItem = group.Key;
            BeginBatchUpdate(rowItem);
            try
            {
                foreach (var c in group) SetCellText(c.Item, c.Column, string.Empty);
            }
            finally
            {
                EndBatchUpdate(rowItem);
            }
        }
    }

    public static void CopySelectionToClipboard(DataGrid grid)
    {
        var cells = grid.SelectedCells.Where(c => c.IsValid && c.Column != null).ToList();
        if (cells.Count == 0)
            return;

        var indexByItem = new Dictionary<object, int>(grid.Items.Count);
        for (int i = 0; i < grid.Items.Count; i++)
            indexByItem[grid.Items[i]] = i;

        var rows = cells.Select(c => indexByItem.TryGetValue(c.Item, out var idx) ? idx : -1)
            .Where(i => i >= 0).Distinct().OrderBy(i => i).ToList();
        var columns = cells.Select(c => c.Column).Distinct().OrderBy(c => c.DisplayIndex).ToList();
        if (rows.Count == 0 || columns.Count == 0)
            return;

        var sb = new StringBuilder();
        for (int r = 0; r < rows.Count; r++)
        {
            var item = grid.Items[rows[r]];
            if (r > 0)
                sb.Append('\n');
            sb.Append(string.Join('\t', columns.Select(col => GetCellText(item, col))));
        }

        Clipboard.SetText(sb.ToString());
        DataGridClipboardSupport.ShowCopyMarker(grid, cells);
        ShellNotificationService.Instance.ShowInfo(
            "Portapapeles",
            cells.Count == 1 ? "1 celda copiada al portapapeles." : $"{cells.Count:N0} celdas copiadas al portapapeles.");
    }
}
