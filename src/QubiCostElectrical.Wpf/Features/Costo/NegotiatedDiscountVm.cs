using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>Fila editable de descuento negociado por código de equipo/material específico
/// (celdas, transformador, generador...), independiente del factor de presentación por categoría.</summary>
public sealed class NegotiatedDiscountVm : ViewModelBase
{
    private readonly Action? onChanged;
    private readonly Func<bool>? canEdit;
    private string codigo;
    private string descripcion;
    private decimal descuentoPct;

    public NegotiatedDiscountVm(NegotiatedDiscountRow row, Action? onChanged, Func<bool>? canEdit = null)
    {
        Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("N") : row.Id;
        codigo = row.Codigo ?? "";
        descripcion = row.Descripcion ?? "";
        descuentoPct = row.DescuentoPct;
        this.onChanged = onChanged;
        this.canEdit = canEdit;
        RaiseValidation();
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public string Id { get; }

    public string Codigo
    {
        get => codigo;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref codigo, value ?? ""))
            {
                RaiseValidation();
                onChanged?.Invoke();
            }
        }
    }

    public string Descripcion
    {
        get => descripcion;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref descripcion, value ?? ""))
                onChanged?.Invoke();
        }
    }

    public decimal DescuentoPct
    {
        // ClampPct ya garantiza [0,100]; no hace falta revalidar (mismo criterio que
        // StaffingRoleVm.TarifaMensualClp/DedicacionPct/Meses — ver su comentario).
        get => descuentoPct;
        set
        {
            if (!AllowEdit) return;
            if (SetProperty(ref descuentoPct, ClampPct(value)))
                onChanged?.Invoke();
        }
    }

    /// <summary>Vacío cuando la fila es válida; mensaje del primer error de NegotiatedDiscountRow.Validate()
    /// en caso contrario. Sin código, la fila queda inerte (nunca hace match en el motor) pero se
    /// avisa igual para que no quede olvidada a medio llenar.</summary>
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

    public NegotiatedDiscountRow ToRow() => new()
    {
        Id = Id,
        Codigo = Codigo,
        Descripcion = Descripcion,
        DescuentoPct = DescuentoPct
    };
}
