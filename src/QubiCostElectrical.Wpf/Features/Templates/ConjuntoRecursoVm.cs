using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Templates;

public sealed class MaestroOption
{
    public string Id { get; set; } = "";
    public string Display { get; set; } = "";
}

public sealed class ConjuntoRecursoVm : ViewModelBase
{
    private readonly ConjuntoRecursoRow source;
    private readonly Action onValueChanged;
    private readonly Action? onMaestroChanged;
    private readonly Func<string, MaterialRow?>? lookupMaterial;

    public static readonly string[] FormulaOperators = { "=", "+", "-", "*", "/" };
    public static readonly string[] LogicaOptions = { "=", "+", "-", "*", "/" };

    public ConjuntoRecursoVm(ConjuntoRecursoRow source, Action onValueChanged, Action? onMaestroChanged = null, Func<string, MaterialRow?>? lookupMaterial = null)
    {
        this.source = source;
        this.onValueChanged = onValueChanged;
        this.onMaestroChanged = onMaestroChanged;
        this.lookupMaterial = lookupMaterial;
        ParseFormula();
        Validate();
    }

    public ConjuntoRecursoRow Source => source;

    private void ParseFormula()
    {
        var f = source.Formula?.Trim();
        if (string.IsNullOrWhiteSpace(f) || f.Equals("MAESTRO", StringComparison.OrdinalIgnoreCase))
        {
            source.FormulaOperator = "=";
            source.Formula = "MAESTRO";
            return;
        }
        var parts = f.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[0].Equals("MAESTRO", StringComparison.OrdinalIgnoreCase))
        {
            var rawOp = parts[1] switch { "×" => "*", "÷" => "/", _ => parts[1] };
            source.FormulaOperator = rawOp;
            if (decimal.TryParse(parts[2], out var factor))
                source.Factor = factor;
            source.Formula = f;
        }
    }

    private void RebuildFormula()
    {
        source.Formula = source.FormulaOperator == "="
            ? "MAESTRO"
            : $"MAESTRO {source.FormulaOperator} {source.Factor}";
        OnPropertyChanged(nameof(FormulaAuto));
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(source.Codigo))
            errors.Add("Código vacío");
        if (source.TipoCalculo == TipoCalculoParametrico.Formula && source.FormulaOperator != "=" && source.Factor <= 0m)
            errors.Add("Factor debe ser mayor que cero");
        if (source.TipoCalculo == TipoCalculoParametrico.Formula && !string.IsNullOrWhiteSpace(source.Formula))
        {
            var err = FormulaEvaluator.Validate(source.Formula, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["MAESTRO"] = 100 });
            if (!string.IsNullOrEmpty(err))
                errors.Add($"Fórmula inválida: {err}");
        }
        HasValidationErrors = errors.Count > 0;
        ValidationErrors = string.Join("; ", errors);
    }

    public bool HasValidationErrors { get; private set; }
    public string ValidationErrors { get; private set; } = "";

    public string Id => source.Id;

    public ApuTipoRecurso Tipo
    {
        get => source.Tipo;
        set
        {
            if (source.Tipo != value)
            {
                source.Tipo = value;
                OnPropertyChanged();
                onValueChanged();
            }
        }
    }

    public string Codigo
    {
        get => source.Codigo;
        set
        {
            if (source.Codigo != value)
            {
                source.Codigo = value ?? "";
                OnPropertyChanged();

                if (lookupMaterial != null)
                {
                    var mat = lookupMaterial(source.Codigo);
                    if (mat != null)
                    {
                        source.Descripcion = mat.Descripcion;
                        source.Unidad = mat.Unidad;
                        OnPropertyChanged(nameof(Descripcion));
                        OnPropertyChanged(nameof(Unidad));
                    }
                }

                Validate();
                OnPropertyChanged(nameof(HasValidationErrors));
                OnPropertyChanged(nameof(ValidationErrors));
                onValueChanged();
            }
        }
    }

    public string Descripcion
    {
        get => source.Descripcion;
        set
        {
            if (source.Descripcion != value)
            {
                source.Descripcion = value ?? "";
                OnPropertyChanged();
                onValueChanged();
            }
        }
    }

    public string Unidad
    {
        get => source.Unidad;
        set
        {
            if (source.Unidad != value)
            {
                source.Unidad = value ?? "";
                OnPropertyChanged();
                onValueChanged();
            }
        }
    }

    public decimal Rendimiento
    {
        get => source.Rendimiento;
        set
        {
            if (source.Rendimiento != value)
            {
                source.Rendimiento = value;
                OnPropertyChanged();
                onValueChanged();
            }
        }
    }

    public decimal Factor
    {
        get => source.Factor;
        set
        {
            if (source.Factor != value)
            {
                source.Factor = value;
                OnPropertyChanged();
                RebuildFormula();
                OnPropertyChanged(nameof(Formula));
                OnPropertyChanged(nameof(FormulaAuto));
                OnPropertyChanged(nameof(FormulaPreview));
                Validate();
                OnPropertyChanged(nameof(HasValidationErrors));
                OnPropertyChanged(nameof(ValidationErrors));
                onValueChanged();
            }
        }
    }

    public decimal PrecioUf
    {
        get => source.PrecioUf;
        set
        {
            if (source.PrecioUf != value)
            {
                source.PrecioUf = value;
                OnPropertyChanged();
                onValueChanged();
            }
        }
    }

    public decimal MermaPct
    {
        get => source.MermaPct;
        set
        {
            if (source.MermaPct != value)
            {
                source.MermaPct = value;
                OnPropertyChanged();
                onValueChanged();
            }
        }
    }

    public TipoCalculoParametrico TipoCalculo
    {
        get => source.TipoCalculo;
        set
        {
            if (source.TipoCalculo != value)
            {
                source.TipoCalculo = value;
                OnPropertyChanged();
                if (value == TipoCalculoParametrico.Espaciamiento && source.Espaciamiento <= 0m)
                    source.Espaciamiento = 1m;
                OnPropertyChanged(nameof(Espaciamiento));
                Validate();
                OnPropertyChanged(nameof(HasValidationErrors));
                OnPropertyChanged(nameof(ValidationErrors));
                onValueChanged();
            }
        }
    }

    public decimal Espaciamiento
    {
        get => source.Espaciamiento;
        set
        {
            if (source.Espaciamiento != value)
            {
                source.Espaciamiento = value <= 0m ? 1m : value;
                OnPropertyChanged();
                onValueChanged();
            }
        }
    }

    public bool EsMaestro
    {
        get => source.EsMaestro;
        set
        {
            if (source.EsMaestro != value)
            {
                source.EsMaestro = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMaster));
                OnPropertyChanged(nameof(IsSlave));
                if (value)
                {
                    source.TipoCalculo = TipoCalculoParametrico.Factor;
                }
                OnPropertyChanged(nameof(TipoCalculo));
                onValueChanged();
                onMaestroChanged?.Invoke();
            }
        }
    }

    public string MaestroRecursoId
    {
        get => source.MaestroRecursoId;
        set
        {
            var newValue = value ?? "";
            if (source.MaestroRecursoId != newValue)
            {
                if (string.IsNullOrWhiteSpace(newValue) && !string.IsNullOrWhiteSpace(source.MaestroRecursoId))
                    return;
                source.MaestroRecursoId = newValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSlave));
                onValueChanged();
                onMaestroChanged?.Invoke();
            }
        }
    }

    public void RefreshMaestroBinding()
    {
        OnPropertyChanged(nameof(MaestroRecursoId));
    }

    public string FormulaOperator
    {
        get => source.FormulaOperator;
        set
        {
            if (source.FormulaOperator != value)
            {
                source.FormulaOperator = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowFactor));
                RebuildFormula();
                OnPropertyChanged(nameof(Formula));
                OnPropertyChanged(nameof(FormulaAuto));
                OnPropertyChanged(nameof(FormulaPreview));
                Validate();
                OnPropertyChanged(nameof(HasValidationErrors));
                OnPropertyChanged(nameof(ValidationErrors));
                onValueChanged();
            }
        }
    }

    public bool ShowFactor => source.FormulaOperator != "=";

    public string Formula
    {
        get => source.Formula;
        set
        {
            if (source.Formula != value)
            {
                source.Formula = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormulaAuto));
                OnPropertyChanged(nameof(FormulaPreview));
                ParseFormula();
                OnPropertyChanged(nameof(FormulaOperator));
                OnPropertyChanged(nameof(ShowFactor));
                OnPropertyChanged(nameof(Factor));
                Validate();
                OnPropertyChanged(nameof(HasValidationErrors));
                OnPropertyChanged(nameof(ValidationErrors));
                onValueChanged();
            }
        }
    }

    public string FormulaAuto
    {
        get
        {
            if (source.FormulaOperator == "=") return "MAESTRO";
            var displayOp = source.FormulaOperator switch
            {
                "*" => "×",
                "/" => "÷",
                _ => source.FormulaOperator
            };
            return $"MAESTRO {displayOp} {source.Factor:N2}";
        }
    }

    public string FormulaPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(source.Formula)) return "";
            var err = FormulaEvaluator.Validate(source.Formula, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["MAESTRO"] = 100
            });
            return string.IsNullOrEmpty(err)
                ? "Formula valida"
                : $"Error: {err}";
        }
    }

    public bool IsSlave => !string.IsNullOrWhiteSpace(source.MaestroRecursoId) && !source.EsMaestro;
    public bool IsMaster => source.EsMaestro;

    public string MaestroDisplayName
        => $"{source.Codigo} · {source.Descripcion}";
}
