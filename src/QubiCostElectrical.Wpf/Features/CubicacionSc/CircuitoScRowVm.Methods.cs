using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed partial class CircuitoScRowVm : ViewModelBase
{
    public CircuitoScRowVm(
        CircuitoSCRow row,
        CubicacionScConfig config,
        IReadOnlyList<ZonaProyecto> zonas,
        Action? onEstadoChanged = null,
        Action? onPisosChanged = null,
        Func<bool>? canEdit = null,
        Action? onChanged = null)
    {
        Source = row;
        this.config = config;
        this.zonas = zonas;
        this.onEstadoChanged = onEstadoChanged;
        this.onPisosChanged = onPisosChanged;
        this.canEdit = canEdit;
        this.onChanged = onChanged;
        // Sincroniza N° de conductores y merma con la alim/fase actual desde el catálogo, para que
        // los totales SIEMPRE se multipliquen por lo que corresponde a esa alim (×3/×4/×5 según el
        // catálogo) y por su merma, en vez de quedarse en el valor por defecto del modelo (3,2).
        // Solo tiene efecto si la alim existe en el catálogo; si no, deja los valores tal cual.
        if (config.Sistemas.Any(s => string.Equals(s.Nombre, row.Sistema, StringComparison.OrdinalIgnoreCase)))
            CubicacionSCEngine.ApplySistema(row, row.Sistema, config);
        Artefactos = new ObservableCollection<ArtefactoScRowVm>(
            row.Artefactos.Select(a => new ArtefactoScRowVm(a, canEdit, onChanged)));
    }

    private CircuitoSCRow row => Source;

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    /// <summary>Expuesto para code-behind (LostFocus/combos) antes de mutar.</summary>
    public bool CanEdit => AllowEdit;

    private void NotifyModelChanged() => onChanged?.Invoke();

    public string Id => row.Id;

    public int Numero
    {
        get => row.Numero;
        set
        {
            if (!AllowEdit) return;
            if (row.Numero == value) return;
            row.Numero = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    // "Tipo" = sistema de uso (A=Alumbrado, E=Enchufe, CL=Clima, C=Computación, F=Fuerza).
    // Edición por texto libre en la grilla (ver CubicacionScView.xaml): escribir "e" + Tab
    // normaliza a "E" sin necesitar abrir un combo, para cargar varios circuitos rápido a
    // pura tecla. Si lo tecleado no calza con ningún código válido, se descarta y la celda
    // vuelve a mostrar el valor anterior (OnPropertyChanged fuerza el refresco visual).
    private static readonly string[] TiposFallback = { "A", "E", "CL", "CO", "C", "F" };

    public string Tipo
    {
        get => row.Tipo;
        set
        {
            if (!AllowEdit)
            {
                OnPropertyChanged();
                return;
            }
            var normalizado = NormalizeSistemaCode(value);
            if (normalizado != null && !string.Equals(row.Tipo, normalizado, StringComparison.Ordinal))
            {
                row.Tipo = normalizado;
                NotifyModelChanged();
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(TipoNombre));
        }
    }

    public string TipoNombre
    {
        get
        {
            var match = config.SistemasCircuito?.FirstOrDefault(s => string.Equals(s.Codigo, row.Tipo, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.Nombre))
                return match.Nombre;

            return row.Tipo switch
            {
                "A" => "Alumbrado",
                "E" => "Enchufe",
                "CL" => "Clima",
                "CO" => "Computación",
                "C" => "Computación",
                "F" => "Fuerza",
                _ => row.Tipo
            };
        }
    }

    private string? NormalizeSistemaCode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();

        var lista = config.SistemasCircuito;
        if (lista != null && lista.Count > 0)
        {
            var byCode = lista.FirstOrDefault(s => string.Equals(s.Codigo, trimmed, StringComparison.OrdinalIgnoreCase));
            if (byCode != null) return byCode.Codigo;

            var byName = lista.FirstOrDefault(s => string.Equals(s.Nombre, trimmed, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName.Codigo;
        }

        var fallback = TiposFallback.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
        if (fallback != null) return fallback;

        if (string.Equals(trimmed, "Alumbrado", StringComparison.OrdinalIgnoreCase)) return "A";
        if (string.Equals(trimmed, "Enchufe", StringComparison.OrdinalIgnoreCase)) return "E";
        if (string.Equals(trimmed, "Clima", StringComparison.OrdinalIgnoreCase)) return "CL";
        if (string.Equals(trimmed, "Computacion", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "Computación", StringComparison.OrdinalIgnoreCase)) return "CO";
        if (string.Equals(trimmed, "Fuerza", StringComparison.OrdinalIgnoreCase)) return "F";

        return null;
    }

    private static string? NormalizeCode(string? input, IReadOnlyList<string> validCodes)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        return validCodes.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public string Descripcion
    {
        get => row.Descripcion;
        set
        {
            if (!AllowEdit) return;
            if (row.Descripcion == value) return;
            row.Descripcion = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    // Ubicación / pisos del circuito: texto libre para un solo piso (ej: "1", "-2", "Azotea"),
    // editable directo en la celda de la grilla. Si el circuito pasa por varias zonas del
    // proyecto (ZonaPisos, elegidas en el flyout de la esquina), este texto pasa a ser un
    // resumen de solo lectura con los nombres de esas zonas — ver TienePisos/IsReadOnly abajo.
    public string Ubicacion
    {
        get => row.TienePisos
            ? string.Join(", ", row.PisosTramos.Select(t => string.IsNullOrWhiteSpace(t.ZonaNombre) ? "Sin zona" : t.ZonaNombre).Distinct())
            : row.Ubicacion;
        set
        {
            if (!AllowEdit) return;
            if (row.TienePisos) return; // de solo lectura mientras tenga varias zonas activas
            if (row.Ubicacion == (value ?? "")) return;
            row.Ubicacion = value ?? "";
            TryParseUbicacionRange(row.Ubicacion);
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    private void TryParseUbicacionRange(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || zonas == null) return;

        var sortedZonas = zonas.Where(z => z.Activa).OrderBy(z => z.Orden).ToList();
        if (sortedZonas.Count == 0) return;

        var parts = value.Split(new[] { " al ", " a ", " hasta ", "-" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            string startTerm = parts[0].Trim();
            string endTerm = parts[1].Trim();

            int startIdx = FindZoneIndex(startTerm, sortedZonas);
            int endIdx = FindZoneIndex(endTerm, sortedZonas);

            if (startIdx >= 0 && endIdx >= 0)
            {
                int min = Math.Min(startIdx, endIdx);
                int max = Math.Max(startIdx, endIdx);

                row.PisosTramos.Clear();
                for (int i = min; i <= max; i++)
                {
                    var z = sortedZonas[i];
                    row.PisosTramos.Add(new CircuitoPisoTramo
                    {
                        ZonaId = z.Id,
                        ZonaNombre = z.Nombre,
                        MetrosBandeja = 0,
                        MetrosTuberia = 0
                    });
                }

                onPisosChanged?.Invoke();
            }
        }
    }

    private int FindZoneIndex(string term, IReadOnlyList<ZonaProyecto> sortedZonas)
    {
        term = term.Trim().ToLowerInvariant();

        for (int i = 0; i < sortedZonas.Count; i++)
        {
            if (string.Equals(sortedZonas[i].Nombre, term, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        bool termIsBasement = IsBasement(term);
        bool termIsFloor = IsFloor(term);
        string termNum = ExtractDigits(term);

        for (int i = 0; i < sortedZonas.Count; i++)
        {
            string name = sortedZonas[i].Nombre.ToLowerInvariant();

            string normTerm = NormalizeAlphanumeric(term);
            string normName = NormalizeAlphanumeric(name);
            if (normName.Contains(normTerm) || normTerm.Contains(normName))
                return i;

            if (!string.IsNullOrEmpty(termNum))
            {
                bool nameIsBasement = IsBasement(name);
                bool nameIsFloor = IsFloor(name);
                string nameNum = ExtractDigits(name);

                if (termNum == nameNum)
                {
                    if (termIsBasement && nameIsBasement) return i;
                    if (termIsFloor && nameIsFloor) return i;
                }
            }
        }

        return -1;
    }

    private bool IsBasement(string s)
    {
        if (s.Contains("sub")) return true;
        if (s.StartsWith("s"))
        {
            string digits = ExtractDigits(s);
            if (!string.IsNullOrEmpty(digits)) return true;
        }
        return false;
    }

    private bool IsFloor(string s)
    {
        if (s.Contains("piso") || s.Contains("nivel")) return true;
        if (s.StartsWith("p"))
        {
            string digits = ExtractDigits(s);
            if (!string.IsNullOrEmpty(digits)) return true;
        }
        return false;
    }

    private string ExtractDigits(string s)
    {
        return new string(s.Where(char.IsDigit).ToArray());
    }

    private string NormalizeAlphanumeric(string s)
    {
        return new string(s.Where(char.IsLetterOrDigit).ToArray());
    }

    public bool TienePisos => row.TienePisos;

    // Zonas del flyout de la esquina (columna Ubicación / Pisos): se rellenan justo antes de
    // abrir el flyout (PopulateZonaPisos) con las zonas activas del proyecto en su orden, y se
    // confirman al cerrarlo (CommitZonaPisos). Sin checkbox: una zona queda activa apenas se le
    // escribe un largo > 0.
    public ObservableCollection<ZonaPisoQuickItem> ZonaPisos { get; } = new();

    private bool pisosPopupOpen;
    public bool PisosPopupOpen { get => pisosPopupOpen; set { pisosPopupOpen = value; OnPropertyChanged(); } }

    public void PopulateZonaPisos(IReadOnlyList<ZonaProyecto> zonasProyecto)
    {
        var existentes = row.PisosTramos.ToDictionary(t => t.ZonaId, StringComparer.Ordinal);
        ZonaPisos.Clear();
        foreach (var z in zonasProyecto.Where(z => z.Activa).OrderBy(z => z.Orden))
        {
            existentes.TryGetValue(z.Id, out var tramo);
            var item = new ZonaPisoQuickItem { ZonaId = z.Id, Nombre = z.Nombre };
            item.OnChanged = () => OnPropertyChanged(nameof(ZonaPisosTotal));
            item.MetrosBandeja = tramo?.MetrosBandeja ?? 0m;
            item.MetrosTuberia = tramo?.MetrosTuberia ?? 0m;
            ZonaPisos.Add(item);
        }
        OnPropertyChanged(nameof(ZonaPisosTotal));
    }

    // Total en vivo (m) mostrado en el footer del flyout, sumando todos los tramos de zona.
    public decimal ZonaPisosTotal => ZonaPisos.Sum(z => z.MetrosBandeja + z.MetrosTuberia);

    public void CommitZonaPisos()
    {
        if (!AllowEdit) return;
        var activas = ZonaPisos.Where(z => z.EsActiva).ToList();
        row.PisosTramos = activas
            .Select(z => new CircuitoPisoTramo { ZonaId = z.ZonaId, ZonaNombre = z.Nombre, MetrosBandeja = z.MetrosBandeja, MetrosTuberia = z.MetrosTuberia })
            .ToList();
        // Sin zonas activas: si antes tenía zonas, vuelve a modo texto libre (vacío, para que el
        // usuario escriba a mano de nuevo). Si nunca tuvo, no toca el texto que haya escrito.
        NotifyPisosChanged();
        onPisosChanged?.Invoke();
        NotifyModelChanged();
    }

    // Tras confirmar el flyout de zonas: refresca ubicación (resumen/editable) y todos los totales.
    public void NotifyPisosChanged()
    {
        OnPropertyChanged(nameof(Ubicacion));
        OnPropertyChanged(nameof(TienePisos));
        OnPropertyChanged(nameof(LargoBandeja));
        OnPropertyChanged(nameof(LargoTuberia));
        OnPropertyChanged(nameof(LargoDistribucion));
        OnPropertyChanged(nameof(LargoDerivacion));
        OnPropertyChanged(nameof(TmfCantidad));
        OnPropertyChanged(nameof(TmfMedida));
    }


    public string Estado
    {
        get => row.Estado;
        set
        {
            if (!AllowEdit) return;
            if (row.Estado == value) return;
            row.Estado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LargoDerivacion));
            OnPropertyChanged(nameof(LargoDistribucion));
            onEstadoChanged?.Invoke();
            NotifyModelChanged();
        }
    }

    public decimal DistanciaTablero
    {
        get => row.DistanciaTablero;
        set
        {
            if (!AllowEdit) return;
            if (row.DistanciaTablero == value) return;
            row.DistanciaTablero = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LargoDerivacion));
            NotifyModelChanged();
        }
    }

    public decimal LargoCircuito
    {
        get => row.LargoCircuito;
        set
        {
            if (!AllowEdit) return;
            if (row.LargoCircuito == value) return;
            row.LargoCircuito = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LargoDistribucion));
            OnPropertyChanged(nameof(LargoTuberia));
            OnPropertyChanged(nameof(LargoBandeja));
            NotifyModelChanged();
        }
    }

    // "Sistema" (campo del dominio) = FASE de alimentación (M=Monofásico, T=Trifásico,
    // EM=Emergencia). Edición por texto libre igual que Tipo: escribir "t" + Tab normaliza a
    // "T" y aplica NumConductores/Merma vía CubicacionSCEngine.ApplySistema. Si lo tecleado no
    // calza con ningún código válido, se descarta y la celda vuelve a mostrar el valor anterior.
    private static readonly string[] FasesValidas = { "M", "T", "EM" };

    public string Fase
    {
        get => row.Sistema;
        set
        {
            if (!AllowEdit)
            {
                OnPropertyChanged();
                return;
            }
            var normalizado = NormalizeCode(value, FasesValidas);
            if (normalizado != null && !string.Equals(row.Sistema, normalizado, StringComparison.Ordinal))
            {
                CubicacionSCEngine.ApplySistema(row, normalizado, config);
                OnPropertyChanged(nameof(NumConductores));
                OnPropertyChanged(nameof(MermaPct));
                OnPropertyChanged(nameof(LargoDerivacion));
                OnPropertyChanged(nameof(LargoDistribucion));
                NotifyModelChanged();
            }
            OnPropertyChanged();
        }
    }

    public string FaseRst
    {
        get => string.IsNullOrWhiteSpace(row.FaseRst) ? CuadroCargasSecEngine.ResolveCircuitPhase(row, row.Numero - 1) : row.FaseRst;
        set
        {
            if (!AllowEdit) return;
            string upper = (value ?? "").Trim().ToUpperInvariant();
            if (upper is "R" or "S" or "T" or "R-S-T" or "")
            {
                if (row.FaseRst != upper)
                {
                    row.FaseRst = upper;
                    OnPropertyChanged();
                    NotifyModelChanged();
                }
            }
        }
    }

    public decimal PotenciaW
    {
        get => row.PotenciaW;
        set
        {
            if (!AllowEdit) return;
            if (row.PotenciaW == value) return;
            row.PotenciaW = Math.Max(0m, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PotenciaEstimadaW));
            OnPropertyChanged(nameof(PotenciaEstimadaKw));
            NotifyModelChanged();
        }
    }

    public decimal PotenciaEstimadaW => CuadroCargasSecEngine.EstimateCircuitPowerW(row);
    public decimal PotenciaEstimadaKw => Math.Round(PotenciaEstimadaW / 1000m, 2);

    public string TipoRecorrido
    {
        get => row.TipoRecorrido;
        set
        {
            if (!AllowEdit) return;
            if (row.TipoRecorrido == value) return;
            row.TipoRecorrido = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EsPorTuberia));
            OnPropertyChanged(nameof(EsPorBandeja));
            OnPropertyChanged(nameof(LargoTuberia));
            OnPropertyChanged(nameof(LargoBandeja));
            NotifyModelChanged();
        }
    }

    public bool EsPorTuberia => !row.EsPorBandeja;
    public bool EsPorBandeja => row.EsPorBandeja;

    public string TipoTuberia
    {
        get => row.TipoTuberia;
        set
        {
            if (!AllowEdit) return;
            if (row.TipoTuberia == value) return;
            row.TipoTuberia = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    // Ducto Ø: edición por texto libre. Escribir solo el número ("32") lo normaliza a "32mm"
    // — sin el sufijo, CubicacionSCEngine.Aggregate/HasFlexiblesWithoutConfig (buscan por
    // Diametro en config.Flexibles) no encontrarían coincidencia y el TMF quedaría sin
    // calcular en silencio.
    public string SeccionTuberia
    {
        get => row.SeccionTuberia;
        set
        {
            if (!AllowEdit)
            {
                OnPropertyChanged();
                return;
            }
            var normalizado = NormalizeDiametro(value);
            if (row.SeccionTuberia != normalizado)
            {
                row.SeccionTuberia = normalizado;
                OnPropertyChanged(nameof(TmfMedida));
                OnPropertyChanged(nameof(TmfCantidad));
                NotifyModelChanged();
            }
            OnPropertyChanged();
        }
    }

    private string NormalizeDiametro(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return row.SeccionTuberia;
        var trimmed = input.Trim();
        var candidate = trimmed.EndsWith("mm", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "mm";
        return config.DiametrosTuberia.FirstOrDefault(d => string.Equals(d, candidate, StringComparison.OrdinalIgnoreCase))
               ?? candidate;
    }

    public string TipoBandeja
    {
        get => row.TipoBandeja;
        set
        {
            if (!AllowEdit) return;
            if (row.TipoBandeja == value) return;
            row.TipoBandeja = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    public decimal AnchoBandeja
    {
        get => row.AnchoBandeja;
        set
        {
            if (!AllowEdit) return;
            if (row.AnchoBandeja == value) return;
            row.AnchoBandeja = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    public string ConductorDerivacion
    {
        get => row.ConductorDerivacion;
        set
        {
            if (!AllowEdit) return;
            if (row.ConductorDerivacion == value) return;
            row.ConductorDerivacion = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    public string SeccionDerivacion
    {
        get => row.SeccionDerivacion;
        set
        {
            if (!AllowEdit) return;
            if (row.SeccionDerivacion == value) return;
            row.SeccionDerivacion = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    public string ConductorDistribucion
    {
        get => row.ConductorDistribucion;
        set
        {
            if (!AllowEdit) return;
            if (row.ConductorDistribucion == value) return;
            row.ConductorDistribucion = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    public string SeccionDistribucion
    {
        get => row.SeccionDistribucion;
        set
        {
            if (!AllowEdit) return;
            if (row.SeccionDistribucion == value) return;
            row.SeccionDistribucion = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    public decimal NumConductores
    {
        get => row.NumConductores;
        set
        {
            if (!AllowEdit) return;
            if (row.NumConductores == value) return;
            row.NumConductores = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LargoDerivacion));
            OnPropertyChanged(nameof(LargoDistribucion));
            NotifyModelChanged();
        }
    }

    public decimal MermaPct
    {
        get => row.MermaPct;
        set
        {
            if (!AllowEdit) return;
            if (row.MermaPct == value) return;
            row.MermaPct = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LargoDerivacion));
            OnPropertyChanged(nameof(LargoDistribucion));
            NotifyModelChanged();
        }
    }

    public decimal Cajas58
    {
        get => row.Cajas58;
        set
        {
            if (!AllowEdit) return;
            if (row.Cajas58 == value) return;
            row.Cajas58 = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalCajas));
            NotifyModelChanged();
        }
    }

    public decimal Cajas100x100
    {
        get => row.Cajas100x100;
        set
        {
            if (!AllowEdit) return;
            if (row.Cajas100x100 == value) return;
            row.Cajas100x100 = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalCajas));
            NotifyModelChanged();
        }
    }

    public decimal Cajas100x65
    {
        get => row.Cajas100x65;
        set
        {
            if (!AllowEdit) return;
            if (row.Cajas100x65 == value) return;
            row.Cajas100x65 = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalCajas));
            NotifyModelChanged();
        }
    }

    public decimal MontajesAlumbrado
    {
        get => row.MontajesAlumbrado;
        set
        {
            if (!AllowEdit) return;
            if (row.MontajesAlumbrado == value) return;
            row.MontajesAlumbrado = value;
            OnPropertyChanged();
            NotifyModelChanged();
        }
    }

    // El terminal flexible aplica al tramo de tubería: hay TMF si ese tramo tiene metraje.
    public int TmfCantidad => row.LargoTuberia > 0 ? 1 : 0;
    public string TmfMedida => row.LargoTuberia > 0 ? SeccionTuberia : "—";

    public int TotalCajas => row.TotalCajas;

    public decimal LargoDerivacion => row.LargoDerivacion;
    public decimal LargoDistribucion => row.LargoDistribucion;
    // L. PVC → metraje del tramo de tubería (Total M PVC = este largo × conductores). Independiente
    // del tramo de bandeja. Mantiene LargoCircuito = suma de ambos tramos (largo total, legacy).
    public decimal LargoTuberia
    {
        get => row.LargoTuberia;
        set
        {
            if (!AllowEdit) return;
            if (row.MetrosTuberia != value)
            {
                row.MetrosTuberia = value;
                row.LargoCircuito = row.MetrosBandeja + row.MetrosTuberia;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LargoDerivacion));
                OnPropertyChanged(nameof(TmfCantidad));
                OnPropertyChanged(nameof(TmfMedida));
                NotifyModelChanged();
            }
        }
    }
    // L. Band → metraje del tramo de bandeja (Total M Band = este largo × conductores).
    public decimal LargoBandeja
    {
        get => row.LargoBandeja;
        set
        {
            if (!AllowEdit) return;
            if (row.MetrosBandeja != value)
            {
                row.MetrosBandeja = value;
                row.LargoCircuito = row.MetrosBandeja + row.MetrosTuberia;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LargoDistribucion));
                NotifyModelChanged();
            }
        }
    }

    public ObservableCollection<ArtefactoScRowVm> Artefactos { get; }

    // Resumen para el botón de la columna ARTEFACTOS de la grilla: total de unidades y en
    // cuántas zonas del proyecto están repartidas, para el control por piso.
    public bool TieneArtefactos => row.Artefactos.Count > 0;
    public int ArtefactosCantidadTotal => (int)Math.Round(row.Artefactos.Sum(a => a.Cantidad));
    public int ArtefactosZonasConteo =>
        row.Artefactos.Where(a => a.Cantidad > 0)
            .Select(a => a.ZonaId ?? "")
            .Distinct()
            .Count();

    // Texto del botón de la columna ARTEFACTOS × ZONA.
    public string ArtefactosResumen => TieneArtefactos
        ? $"{ArtefactosCantidadTotal} u · {ArtefactosZonasConteo} z"
        : "+ Artefactos";

    public void AddArtefacto(string nombre, string categoria, decimal cantidad)
    {
        if (!AllowEdit) return;
        if (string.IsNullOrWhiteSpace(nombre)) return;
        var artefacto = new ArtefactoSCRow { Nombre = nombre.Trim(), Categoria = categoria, Cantidad = cantidad <= 0 ? 1 : cantidad };
        row.Artefactos.Add(artefacto);
        Artefactos.Add(new ArtefactoScRowVm(artefacto, canEdit, onChanged));
        NotifyArtefactosResumen();
        // Dirty: lo marca el comando del ViewModel (AddArtefactoCommand).
    }

    public void RemoveArtefacto(ArtefactoScRowVm vm)
    {
        if (!AllowEdit) return;
        row.Artefactos.Remove(vm.Source);
        Artefactos.Remove(vm);
        NotifyArtefactosResumen();
        // Dirty: lo marca el comando del ViewModel (RemoveArtefactoCommand).
    }

    // Reconstruye la colección observable desde el modelo tras editar los artefactos por zona
    // en el diálogo (que muta row.Artefactos directamente).
    public void ReloadArtefactos()
    {
        Artefactos.Clear();
        foreach (var a in row.Artefactos)
            Artefactos.Add(new ArtefactoScRowVm(a, canEdit, onChanged));
        NotifyArtefactosResumen();
    }

    private void NotifyArtefactosResumen()
    {
        OnPropertyChanged(nameof(TieneArtefactos));
        OnPropertyChanged(nameof(ArtefactosCantidadTotal));
        OnPropertyChanged(nameof(ArtefactosZonasConteo));
        OnPropertyChanged(nameof(ArtefactosResumen));
    }

    /// <summary>
    /// Pega una plantilla de circuito sobre esta fila (mantiene Id y N°). Usado por Ctrl+V
    /// tras copiar un circuito — no agrega fila nueva.
    /// </summary>
    public void ApplyClipboardFrom(CircuitoSCRow template)
    {
        if (!AllowEdit) return;
        row.Tipo = template.Tipo;
        row.Estado = template.Estado;
        row.Descripcion = template.Descripcion;
        row.Ubicacion = template.Ubicacion;
        row.DistanciaTablero = template.DistanciaTablero;
        row.LargoCircuito = template.LargoCircuito;
        row.MetrosBandeja = template.MetrosBandeja;
        row.MetrosTuberia = template.MetrosTuberia;
        row.PisosTramos = template.PisosTramos.Select(t => new CircuitoPisoTramo
        {
            ZonaId = t.ZonaId,
            ZonaNombre = t.ZonaNombre,
            MetrosBandeja = t.MetrosBandeja,
            MetrosTuberia = t.MetrosTuberia,
        }).ToList();
        row.TipoRecorrido = template.TipoRecorrido;
        row.TipoTuberia = template.TipoTuberia;
        row.SeccionTuberia = template.SeccionTuberia;
        row.TipoBandeja = template.TipoBandeja;
        row.AnchoBandeja = template.AnchoBandeja;
        row.ConductorDerivacion = template.ConductorDerivacion;
        row.SeccionDerivacion = template.SeccionDerivacion;
        row.ConductorDistribucion = template.ConductorDistribucion;
        row.SeccionDistribucion = template.SeccionDistribucion;
        row.NumConductores = template.NumConductores;
        row.Sistema = template.Sistema;
        row.MermaPct = template.MermaPct;
        row.Artefactos = template.Artefactos.Select(a => new ArtefactoSCRow
        {
            Nombre = a.Nombre,
            Categoria = a.Categoria,
            Cantidad = a.Cantidad,
            ZonaId = a.ZonaId,
        }).ToList();
        row.SensoresMovimiento = template.SensoresMovimiento;
        row.Senaleticas = template.Senaleticas;
        row.CintaLedMts = template.CintaLedMts;
        row.MontajesAlumbrado = template.MontajesAlumbrado;
        row.Cajas58 = template.Cajas58;
        row.Cajas100x100 = template.Cajas100x100;
        row.Cajas100x65 = template.Cajas100x65;

        ReloadArtefactos();
        OnPropertyChanged(nameof(Tipo));
        OnPropertyChanged(nameof(Fase));
        OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Ubicacion));
        OnPropertyChanged(nameof(TienePisos));
        OnPropertyChanged(nameof(LargoBandeja));
        OnPropertyChanged(nameof(LargoTuberia));
        OnPropertyChanged(nameof(LargoDistribucion));
        OnPropertyChanged(nameof(LargoDerivacion));
        OnPropertyChanged(nameof(ConductorDistribucion));
        OnPropertyChanged(nameof(SeccionDistribucion));
        OnPropertyChanged(nameof(ConductorDerivacion));
        OnPropertyChanged(nameof(SeccionDerivacion));
        OnPropertyChanged(nameof(TipoTuberia));
        OnPropertyChanged(nameof(SeccionTuberia));
        OnPropertyChanged(nameof(MontajesAlumbrado));
        OnPropertyChanged(nameof(Cajas58));
        OnPropertyChanged(nameof(Cajas100x100));
        OnPropertyChanged(nameof(Cajas100x65));
        OnPropertyChanged(nameof(TmfCantidad));
        OnPropertyChanged(nameof(TmfMedida));
        // Dirty: lo marca el comando del ViewModel (PasteCircuitoCommand).
    }
}
