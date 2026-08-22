using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>Fila editable de dotación indirecta por cargo (Administrador, Supervisor, Capataz...)
/// con tarifa mensual, % de dedicación y meses — se suma automáticamente a Gastos Operacionales.</summary>
public sealed class StaffingRoleVm : ViewModelBase
{
    private readonly Action? onChanged;
    private string cargo;
    private decimal tarifaMensualClp;
    private decimal dedicacionPct;
    private int meses;

    public StaffingRoleVm(StaffingRoleRow row, Action? onChanged)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        cargo = row.Cargo ?? "";
        tarifaMensualClp = row.TarifaMensualClp;
        dedicacionPct = row.DedicacionPct;
        meses = row.Meses;
        this.onChanged = onChanged;
        RaiseValidation();
    }

    public string Id { get; }

    public string Cargo
    {
        get => cargo;
        set
        {
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
            if (SetProperty(ref dedicacionPct, Math.Clamp(value, 0m, 100m)))
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
            if (SetProperty(ref meses, Math.Max(0, value)))
            {
                RaiseTotal();
                onChanged?.Invoke();
            }
        }
    }

    public decimal TotalClp => TarifaMensualClp * (DedicacionPct / 100m) * Meses;
    public string TotalLabel => $"$ {TotalClp:N0}";

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
