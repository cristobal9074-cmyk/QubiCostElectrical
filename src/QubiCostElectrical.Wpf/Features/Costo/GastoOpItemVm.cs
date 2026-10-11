using System;
using System.Globalization;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class GastoOpItemVm : ViewModelBase
{
    private readonly Action? onChanged;
    private readonly Func<bool>? canEdit;
    private string concepto;
    private string unidad;
    private decimal cantidad;
    private decimal precioUnitarioClp;

    public GastoOpItemVm(GastoOperacionalItemRow row, Action? onChanged, Func<bool>? canEdit = null)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        concepto = row.Concepto ?? "";
        unidad = row.Unidad ?? "";
        cantidad = row.Cantidad;
        precioUnitarioClp = row.PrecioUnitarioClp;
        this.onChanged = onChanged;
        this.canEdit = canEdit;
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public string Id { get; }

    public string Concepto
    {
        get => concepto;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref concepto, value ?? ""))
                onChanged?.Invoke();
        }
    }

    public string Unidad
    {
        get => unidad;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref unidad, value ?? ""))
                onChanged?.Invoke();
        }
    }

    public decimal Cantidad
    {
        get => cantidad;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref cantidad, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(CantidadTexto));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalLabel));
                OnPropertyChanged(nameof(IsZeroTotal));
                onChanged?.Invoke();
            }
        }
    }

    public string CantidadTexto
    {
        get => cantidad == decimal.Truncate(cantidad)
            ? cantidad.ToString("N0", CultureInfo.CurrentCulture)
            : cantidad.ToString("0.##", CultureInfo.CurrentCulture);
        set
        {
            if (!AllowEdit)
            {
                OnPropertyChanged();
                return;
            }

            if (!TryParseCantidad(value, out var v))
            {
                OnPropertyChanged();
                return;
            }

            Cantidad = v;
            OnPropertyChanged();
        }
    }

    public decimal PrecioUnitarioClp
    {
        get => precioUnitarioClp;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref precioUnitarioClp, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(PrecioTexto));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalLabel));
                OnPropertyChanged(nameof(IsZeroTotal));
                onChanged?.Invoke();
            }
        }
    }

    public string PrecioTexto
    {
        get => precioUnitarioClp.ToString("N0", CultureInfo.CurrentCulture);
        set
        {
            if (!AllowEdit)
            {
                OnPropertyChanged();
                return;
            }

            if (!TryParseClp(value, out var v))
            {
                OnPropertyChanged();
                return;
            }

            PrecioUnitarioClp = decimal.Round(v, 0, MidpointRounding.AwayFromZero);
            OnPropertyChanged();
        }
    }

    public decimal Total => Math.Round(Cantidad * PrecioUnitarioClp);
    public string TotalLabel => CurrencyFormat.Clp(Total);
    public bool IsZeroTotal => Total <= 0;

    public GastoOperacionalItemRow ToRow() => new()
    {
        Id = Id,
        Concepto = Concepto,
        Unidad = Unidad,
        Cantidad = Cantidad,
        PrecioUnitarioClp = PrecioUnitarioClp,
    };

    private static bool TryParseCantidad(string? text, out decimal value)
    {
        value = 0m;
        var s = (text ?? "").Trim().Replace(" ", "");
        if (s.Length == 0)
            return true;
        var dots = 0;
        foreach (var ch in s)
        {
            if (ch == '.')
                dots++;
        }

        if (dots > 1)
            s = s.Replace(".", "");
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseClp(string? text, out decimal value)
    {
        value = 0m;
        var raw = (text ?? "").Trim();
        if (raw.Length == 0)
            return true;

        var digits = new char[raw.Length];
        var n = 0;
        foreach (var ch in raw)
        {
            if (char.IsDigit(ch))
                digits[n++] = ch;
        }

        if (n == 0)
            return false;
        return decimal.TryParse(new string(digits, 0, n), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
