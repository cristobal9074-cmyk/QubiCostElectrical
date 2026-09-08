using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>Fila editable de dotación indirecta por cargo (Administrador, Supervisor, Capataz...)
/// con tarifa mensual, % de dedicación y meses — se suma automáticamente a Gastos Operacionales.</summary>
public sealed class StaffingRoleVm : ViewModelBase
{
    private readonly Action? onChanged;
    private readonly Func<bool>? canEdit;
    private string cargo;
    private decimal tarifaMensualClp;
    private decimal dedicacionPct;
    private int meses;

    public StaffingRoleVm(StaffingRoleRow row, Action? onChanged, Func<bool>? canEdit = null)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        cargo = row.Cargo ?? "";
        tarifaMensualClp = row.TarifaMensualClp;
        dedicacionPct = row.DedicacionPct;
        meses = row.Meses;
        this.onChanged = onChanged;
        this.canEdit = canEdit;
        RaiseValidation();
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public string Id { get; }

    public string Cargo
    {
        get => cargo;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref cargo, value ?? ""))
            {
                RaiseValidation();
                onChanged?.Invoke();
            }
        }
    }

    public decimal TarifaMensualClp
    {
        get => tarifaMensualClp;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref tarifaMensualClp, Math.Max(0m, value)))
            {
                RaiseTotal();
                onChanged?.Invoke();
            }
        }
    }

    public decimal DedicacionPct
    {
        get => dedicacionPct;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref dedicacionPct, ClampPct(value)))
            {
                RaiseTotal();
                onChanged?.Invoke();
            }
        }
    }

    public int Meses
    {
        get => meses;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref meses, Math.Max(0, value)))
            {
                RaiseTotal();
                onChanged?.Invoke();
            }
        }
    }

    public decimal TotalClp => TarifaMensualClp * (DedicacionPct / 100m) * Meses;
    public string TotalLabel => CurrencyFormat.Clp(TotalClp);

    /// <summary>Vacío cuando la fila es válida (ver StaffingRoleRow.Validate()). En la práctica solo
    /// Cargo puede quedar inválido desde la UI: Tarifa/Dedicación/Meses ya se clampan al tipear.</summary>
    public string ValidationHint
    {
        get => validationHint;
        private set => SetProperty(ref validationHint, value);
    }

    private string validationHint = "";

    public bool HasValidationError => ValidationHint.Length > 0;

    private void RaiseValidation()
    {
        var errors = ToRow().Validate();
        ValidationHint = errors.Count > 0 ? errors[0] : "";
        OnPropertyChanged(nameof(HasValidationError));
    }

    private void RaiseTotal()
    {
        OnPropertyChanged(nameof(TotalClp));
        OnPropertyChanged(nameof(TotalLabel));
    }

    public StaffingRoleRow ToRow() => new()
    {
        Id = Id,
        Cargo = Cargo,
        TarifaMensualClp = TarifaMensualClp,
        DedicacionPct = DedicacionPct,
        Meses = Meses
    };
}
