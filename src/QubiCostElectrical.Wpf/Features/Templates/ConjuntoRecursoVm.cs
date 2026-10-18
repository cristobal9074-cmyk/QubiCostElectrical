using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Templates;

// ─── Opción de maestro para el combo "Dividir de" ─────────────────────────────
public sealed class MaestroOptionVm
{
    public MaestroOptionVm(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}

// ─── VM de recurso de conjunto eléctrico (editable) ───────────────────────────
public sealed class ConjuntoRecursoVm : ViewModelBase
{
    private readonly ConjuntoRecursoRow source;
    private readonly Action onChanged;
    private readonly Func<string, MaterialRow?>? lookupMaterial;
    private readonly Func<string, IReadOnlyList<MaestroOptionVm>>? maestroOptions;
    private readonly Action? onStructureChanged;
    private readonly Action<string>? notify;
    private readonly Func<IReadOnlyList<ConjuntoRecursoRow>>? getRecursos;
    private readonly ObservableCollection<MaestroOptionVm> maestrosDisponibles = new();

    public ConjuntoRecursoVm(
        ConjuntoRecursoRow source,
        Action onChanged,
        Func<string, MaterialRow?>? lookupMaterial = null,
        Func<string, IReadOnlyList<MaestroOptionVm>>? maestroOptions = null,
        Action? onStructureChanged = null,
        Action<string>? notify = null,
        Func<IReadOnlyList<ConjuntoRecursoRow>>? getRecursos = null)
    {
        this.source = source;
        this.onChanged = onChanged;
        this.lookupMaterial = lookupMaterial;
        this.maestroOptions = maestroOptions;
        this.onStructureChanged = onStructureChanged;
        this.notify = notify;
        this.getRecursos = getRecursos;
    }

    public ConjuntoRecursoRow Source => source;

    public string Id => source.Id;

    public bool EsEsclavo => !string.IsNullOrWhiteSpace(source.MaestroId);

    /// <summary>La fila es candidata a maestro: no es esclava ni es una operación.</summary>
    public bool EsMaestro => !EsEsclavo && string.IsNullOrWhiteSpace(source.Operacion);

    /// <summary>La fila tiene fórmula (rol Operación).</summary>
    public bool EsOperacion => !string.IsNullOrWhiteSpace(source.Operacion);

    /// <summary>Rol en la plantilla: Maestro (cantidad propia), Esclavo (deriva de un maestro) u Operación (fórmula).</summary>
    public string Rol
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(source.Operacion)) return "Operación";
            return EsEsclavo ? "Esclavo" : "Maestro";
        }
        set
        {
            string target = (value ?? "").Trim();
            if (string.Equals(target, Rol, StringComparison.OrdinalIgnoreCase)) return;

            if (string.Equals(target, "Esclavo", StringComparison.OrdinalIgnoreCase))
            {
                var opciones = maestroOptions?.Invoke(source.Id) ?? Array.Empty<MaestroOptionVm>();
                if (opciones.Count == 0)
                {
                    notify?.Invoke("No hay materiales maestros disponibles: marca primero una fila como Maestro.");
                    OnPropertyChanged(nameof(Rol));
                    return;
                }
                source.Operacion = "";
                source.MaestroId = opciones[0].Id;
                notify?.Invoke($"Vinculada a «{opciones[0].Label}»: cambia «Dividir de» si necesitas otro maestro.");
            }
            else if (string.Equals(target, "Maestro", StringComparison.OrdinalIgnoreCase))
            {
                source.Operacion = "";
                source.MaestroId = "";
            }
            else if (string.Equals(target, "Operación", StringComparison.OrdinalIgnoreCase))
            {
                source.MaestroId = ""; // una operación nunca es esclava
                if (string.IsNullOrWhiteSpace(source.Operacion))
                    notify?.Invoke("Escribe la fórmula en la columna Operación (ej. PAZ * 2 + TEEG500).");
            }
            else
            {
                return;
            }

            NotifyStructureChanged();
            onChanged();
            onStructureChanged?.Invoke();
        }
    }

    /// <summary>Fórmula libre de la fila Operación (vacío = la fila no es operación).</summary>
    public string Operacion
    {
        get => source.Operacion ?? "";
        set
        {
            string nueva = value ?? "";
            if (string.Equals(nueva, source.Operacion ?? "", StringComparison.Ordinal)) return;

            source.Operacion = nueva;
            OnPropertyChanged();

            if (!string.IsNullOrWhiteSpace(nueva))
                ValidarFormulaOperacion(nueva);

            NotifyStructureChanged();
            onChanged();
            onStructureChanged?.Invoke();
        }
    }

    private void ValidarFormulaOperacion(string formula)
    {
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in getRecursos?.Invoke() ?? Array.Empty<ConjuntoRecursoRow>())
        {
            string clave = OperacionFilaService.NormalizeVariableKey(r.Codigo);
            if (clave.Length == 0 || variables.ContainsKey(clave)) continue;
            variables[clave] = 0m;
        }

        var resultado = QuantityFormulaService.Evaluate(formula, variables);
        if (!resultado.Success)
            notify?.Invoke($"Operación inválida: {resultado.Error}");
    }

    /// <summary>Id del maestro del que deriva esta fila (vacío = la fila es maestro).</summary>
    public string MaestroId
    {
        get => source.MaestroId ?? "";
        set
        {
            string nuevo = (value ?? "").Trim();
            if (nuevo.Length > 0)
            {
                var opciones = maestroOptions?.Invoke(source.Id) ?? Array.Empty<MaestroOptionVm>();
                if (!opciones.Any(o => string.Equals(o.Id, nuevo, StringComparison.OrdinalIgnoreCase)))
                {
                    // Candidato inválido (esclavo, MO/HH/HERR, fila de otra plantilla): conservar
                    // el vínculo vigente. Un rechazo nunca borra un maestro válido; solo se
                    // re-bobina la celda al valor real con NotifyStructureChanged.
                    notify?.Invoke("Ese recurso no puede ser maestro: elige una fila maestro de esta plantilla.");
                    NotifyStructureChanged();
                    return;
                }
            }
            if (string.Equals(nuevo, source.MaestroId ?? "", StringComparison.Ordinal)) return;

            source.MaestroId = nuevo;
            NotifyStructureChanged();
            onChanged();
            onStructureChanged?.Invoke();
        }
    }

    /// <summary>
    /// Candidatos a maestro de esta fila para el combo «Dividir de». Colección ESTABLE (misma
    /// referencia siempre): el ItemsSource nunca se reemplaza, solo se reconcilia su contenido.
    /// Si se publicara una lista nueva en cada refresco, el SelectedValue TwoWay del combo se
    /// resetearía a vacío en el cambio y borraría el vínculo recién elegido ("se elimina el
    /// otro maestro").
    /// </summary>
    public IReadOnlyList<MaestroOptionVm> MaestrosDisponibles => maestrosDisponibles;

    /// <summary>Reconcilia la colección en-place: quita los que dejaron de ser candidatos,
    /// agrega/actualiza los nuevos. Sin Clear/Reset: un candidato seleccionado no se toca.</summary>
    private void ActualizarMaestrosDisponibles()
    {
        var opciones = maestroOptions?.Invoke(source.Id) ?? Array.Empty<MaestroOptionVm>();

        for (int i = maestrosDisponibles.Count - 1; i >= 0; i--)
            if (!opciones.Any(o => string.Equals(o.Id, maestrosDisponibles[i].Id, StringComparison.OrdinalIgnoreCase)))
                maestrosDisponibles.RemoveAt(i);

        foreach (var o in opciones)
        {
            int idx = -1;
            for (int i = 0; i < maestrosDisponibles.Count; i++)
                if (string.Equals(maestrosDisponibles[i].Id, o.Id, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }

            if (idx < 0) maestrosDisponibles.Add(o);
            else if (!string.Equals(maestrosDisponibles[idx].Label, o.Label, StringComparison.Ordinal))
                maestrosDisponibles[idx] = o;
        }
    }

    /// <summary>Valor editable de la columna "Paso (m) / Factor" según el modo activo.</summary>
    public decimal PasoFactor
    {
        get => source.TipoCalculo == TipoCalculoParametrico.Espaciamiento ? source.Espaciamiento : source.Factor;
        set
        {
            if (source.TipoCalculo == TipoCalculoParametrico.Espaciamiento)
            {
                Espaciamiento = value; // ya notifica y marca sucio
            }
            else if (source.Factor != value)
            {
                source.Factor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Factor));
                onChanged();
            }
            OnPropertyChanged(nameof(PasoFactor));
        }
    }

    public void NotifyStructureChanged()
    {
        ActualizarMaestrosDisponibles();
        OnPropertyChanged(nameof(Rol));
        OnPropertyChanged(nameof(MaestroId));
        OnPropertyChanged(nameof(EsEsclavo));
        OnPropertyChanged(nameof(EsMaestro));
        OnPropertyChanged(nameof(EsOperacion));
        OnPropertyChanged(nameof(Operacion));
        OnPropertyChanged(nameof(MaestrosDisponibles));
    }

    public ApuTipoRecurso Tipo
    {
        get => source.Tipo;
        set
        {
            if (source.Tipo != value)
            {
                source.Tipo = value;
                OnPropertyChanged();
                onChanged();
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

                onChanged();
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
                onChanged();
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
                onChanged();
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
                onChanged();
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
                onChanged();
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
                onChanged();
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
                onChanged();
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
                // Reset Espaciamiento to sensible default when switching modes
                if (value == TipoCalculoParametrico.Espaciamiento && source.Espaciamiento <= 0m)
                    source.Espaciamiento = 1m;
                OnPropertyChanged(nameof(Espaciamiento));
                OnPropertyChanged(nameof(PasoFactor));
                onChanged();
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
                onChanged();
            }
        }
    }
}
