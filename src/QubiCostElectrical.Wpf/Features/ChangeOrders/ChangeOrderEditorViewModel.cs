using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public sealed partial class ChangeOrderEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _numero = "";

    [ObservableProperty]
    private string _titulo = "";

    [ObservableProperty]
    private string _creadoPor = "";

    [ObservableProperty]
    private string _deltaVentaText = "";

    [ObservableProperty]
    private string _deltaCostoText = "";

    [ObservableProperty]
    private string _costoBaseText = "";

    [ObservableProperty]
    private string _nota = "";

    [ObservableProperty]
    private string? _validationErrorMessage;

    [ObservableProperty]
    private ChangeOrderLineVm? _selectedLine;

    public ObservableCollection<ChangeOrderLineVm> Lineas { get; } = new();

    public ObservableCollection<ChangeOrderMaterialVm> ActiveMateriales { get; } = new();

    public List<ChangeOrderPartidaPickerItem> AvailableContractPartidas { get; set; } = new();

    public bool MuestraUf { get; private set; } = true;

    public decimal UfRate { get; private set; } = PriceConversion.DefaultValorUfClp;

    public decimal GastosGeneralesPct { get; private set; } = 0m;

    public decimal UtilidadPct { get; private set; } = 0m;

    public string EtiquetaMoneda => MuestraUf ? "UF" : "$";

    public string CostoBaseLabel => $"Costo base ({EtiquetaMoneda})";
    public string DeltaCostoLabel => $"Delta costo ({EtiquetaMoneda})";
    public string DeltaVentaLabel => $"Delta venta ({EtiquetaMoneda})";

    public decimal SubtotalContractualClp => Lineas.Where(l => l.EsContractual && !l.EsTitulo).Sum(l => l.SubtotalClp);
    public decimal SubtotalContractualUf => Lineas.Where(l => l.EsContractual && !l.EsTitulo).Sum(l => l.SubtotalUf);
    public decimal SubtotalExtraordinarioClp => Lineas.Where(l => !l.EsContractual && !l.EsTitulo).Sum(l => l.SubtotalClp);
    public decimal SubtotalExtraordinarioUf => Lineas.Where(l => !l.EsContractual && !l.EsTitulo).Sum(l => l.SubtotalUf);

    public decimal TotalAdicionalClp => SubtotalContractualClp + SubtotalExtraordinarioClp;
    public decimal TotalAdicionalUf => SubtotalContractualUf + SubtotalExtraordinarioUf;

    public string SubtotalContractualDisplay => MuestraUf
        ? $"{SubtotalContractualUf:N3} UF"
        : $"$ {SubtotalContractualClp:N0}";

    public string SubtotalExtraordinarioDisplay => MuestraUf
        ? $"{SubtotalExtraordinarioUf:N3} UF"
        : $"$ {SubtotalExtraordinarioClp:N0}";

    public string TotalAdicionalDisplay => MuestraUf
        ? $"{TotalAdicionalUf:N3} UF"
        : $"$ {TotalAdicionalClp:N0}";

    public bool HasLineas => Lineas.Count > 0;
    public bool HasSelectedLine => SelectedLine != null;
    public bool IsSelectedLineTitulo => SelectedLine?.EsTitulo == true;
    public bool IsSelectedLinePartida => SelectedLine != null && !SelectedLine.EsTitulo;

    public int PartidasCount => Lineas.Count(l => !l.EsTitulo);
    public string PartidasCountBadgeText => $"{PartidasCount} partidas en adicional";

    public string SelectedLineItemText => SelectedLine != null ? $"{SelectedLine.Item}.-" : "—";
    public string SelectedLineDescText => SelectedLine?.DescripcionPartida ?? "Seleccione una partida para ver su planilla de zonas";
    public string SelectedLinePuLabel => SelectedLine != null && SelectedLine.EsContractual ? "P.U. Contrato 🔒: " : "P.U. Venta: ";
    public string SelectedLinePuDisplay => SelectedLine != null
        ? (MuestraUf ? $"{SelectedLine.PrecioUnitarioUf:N3} UF" : $"$ {SelectedLine.PrecioUnitarioClp:N0}")
        : "—";
    public string SelectedLineSubtotalDisplay => SelectedLine != null ? SelectedLine.SubtotalDisplay : "—";
    public string SelectedLineCantTotalDisplay => SelectedLine != null ? $"{(SelectedLine.CantidadAdicional % 1 == 0 ? Math.Round(SelectedLine.CantidadAdicional, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : SelectedLine.CantidadAdicional.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture))} {SelectedLine.Unidad}" : "—";

    public void ConfigureCurrency(bool muestraUf, decimal ufRate)
    {
        MuestraUf = muestraUf;
        UfRate = ufRate > 0m ? ufRate : PriceConversion.DefaultValorUfClp;
        OnPropertyChanged(nameof(MuestraUf));
        OnPropertyChanged(nameof(EtiquetaMoneda));
        OnPropertyChanged(nameof(CostoBaseLabel));
        OnPropertyChanged(nameof(DeltaCostoLabel));
        OnPropertyChanged(nameof(DeltaVentaLabel));

        foreach (var l in Lineas)
        {
            l.MuestraUf = muestraUf;
            l.UfRate = UfRate;
        }
        foreach (var m in ActiveMateriales)
        {
            m.MuestraUf = muestraUf;
            m.UfRate = UfRate;
            m.Recalculate();
        }
        RecalculateTotals();
    }

    public void ConfigureMargins(decimal gastosGeneralesPct, decimal utilidadPct)
    {
        GastosGeneralesPct = Math.Max(0m, gastosGeneralesPct);
        UtilidadPct = Math.Max(0m, utilidadPct);
        RecalculateTotals();
    }

    partial void OnNumeroChanged(string value) => ClearError();
    partial void OnTituloChanged(string value) => ClearError();
    partial void OnCreadoPorChanged(string value) => ClearError();
    partial void OnNotaChanged(string value) => ClearError();

    private bool _isLoadingMaterials;

    partial void OnSelectedLineChanged(ChangeOrderLineVm? oldValue, ChangeOrderLineVm? newValue)
    {
        // Guardar materiales de la linea actualmente seleccionada
        if (!_isLoadingMaterials && oldValue != null && !oldValue.EsTitulo)
        {
            oldValue.SetMateriales(ActiveMateriales.Select(m => m.ToModel(oldValue.ItemId)));
        }

        _isLoadingMaterials = true;
        try
        {
            ActiveMateriales.Clear();
            if (newValue != null && !newValue.EsTitulo && newValue.Materiales != null)
            {
                foreach (var mat in newValue.Materiales)
                {
                    var vm = ChangeOrderMaterialVm.FromModel(mat, MuestraUf, UfRate, OnActiveMaterialChanged);
                    vm.OnRecalculateFormulasRequested = RecalculateActiveMaterialFormulas;
                    ActiveMateriales.Add(vm);
                }
            }
        }
        finally
        {
            _isLoadingMaterials = false;
        }

        RecalculateActiveMaterialFormulas();

        if (newValue != null && !newValue.EsContractual && !newValue.EsTitulo && newValue.Materiales.Count > 0)
        {
            newValue.UpdateExtraordinariaPriceFromMateriales();
            newValue.Recalculate();
        }

        RecalculateTotals();
        RefreshSelectedLineHeader();
    }

    public void RefreshSelectedLineHeader()
    {
        OnPropertyChanged(nameof(HasSelectedLine));
        OnPropertyChanged(nameof(IsSelectedLineTitulo));
        OnPropertyChanged(nameof(IsSelectedLinePartida));
        OnPropertyChanged(nameof(PartidasCount));
        OnPropertyChanged(nameof(PartidasCountBadgeText));
        OnPropertyChanged(nameof(SelectedLineItemText));
        OnPropertyChanged(nameof(SelectedLineDescText));
        OnPropertyChanged(nameof(SelectedLinePuLabel));
        OnPropertyChanged(nameof(SelectedLinePuDisplay));
        OnPropertyChanged(nameof(SelectedLineSubtotalDisplay));
        OnPropertyChanged(nameof(SelectedLineCantTotalDisplay));
    }

    private void OnActiveMaterialChanged()
    {
        if (_isLoadingMaterials || SelectedLine == null || SelectedLine.EsTitulo) return;

        // Persistir en SelectedLine.Materiales y recalcular precio unitario/subtotal
        SelectedLine.SetMateriales(ActiveMateriales.Select(m => m.ToModel(SelectedLine.ItemId)));

        RecalculateTotals();
        RefreshSelectedLineHeader();
    }

    public void AddMaterialToActiveLine(string codigo = "", string desc = "", string ud = "UN")
    {
        if (SelectedLine == null || SelectedLine.EsTitulo) return;

        var vm = new ChangeOrderMaterialVm(OnActiveMaterialChanged)
        {
            SuppressNotifications = true,
            Tipo = TipoRecurso.MAT,
            Codigo = codigo,
            Descripcion = desc,
            Unidad = string.IsNullOrWhiteSpace(ud) ? "UN" : ud,
            Cantidad = 0m,
            CantidadFormula = "",
            MatClp = 0m,
            MoClp = 0m,
            GgUtFactor = 1.00m,
            NotaInterna = "",
            MuestraUf = MuestraUf,
            UfRate = UfRate
        };
        vm.OnRecalculateFormulasRequested = RecalculateActiveMaterialFormulas;
        vm.Recalculate();
        vm.SuppressNotifications = false;
        ActiveMateriales.Add(vm);
        RecalculateActiveMaterialFormulas();
    }

    public void RemoveMaterialFromActiveLine(ChangeOrderMaterialVm? mat)
    {
        if (mat != null && ActiveMateriales.Contains(mat))
        {
            ActiveMateriales.Remove(mat);
            RecalculateActiveMaterialFormulas();
        }
    }

    public bool PreloadBaseMaterialsForActiveLine(IEnumerable<CubicacionRow>? baseProjectCubicacion)
    {
        if (SelectedLine == null || SelectedLine.EsTitulo || string.IsNullOrWhiteSpace(SelectedLine.ItemId) || baseProjectCubicacion == null)
            return false;

        var baseRows = baseProjectCubicacion
            .Where(c => string.Equals(c.ItemId, SelectedLine.ItemId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(c.Codigo))
            .ToList();

        if (baseRows.Count == 0) return false;

        _isLoadingMaterials = true;
        try
        {
            ActiveMateriales.Clear();
            foreach (var bRow in baseRows)
            {
                var vm = new ChangeOrderMaterialVm(OnActiveMaterialChanged)
                {
                    SuppressNotifications = true,
                    Tipo = string.IsNullOrWhiteSpace(bRow.Tipo) ? TipoRecurso.MAT : bRow.Tipo,
                    Codigo = bRow.Codigo,
                    Descripcion = bRow.Descripcion,
                    Unidad = string.IsNullOrWhiteSpace(bRow.Unidad) ? "UN" : bRow.Unidad,
                    Cantidad = 0m,
                    MatClp = bRow.MatClpConFallback,
                    MoClp = bRow.MoClpConFallback,
                    GgUtFactor = bRow.GgUtFactor > 0m ? bRow.GgUtFactor : 1.00m,
                    NotaInterna = bRow.NotaInterna ?? "",
                    MuestraUf = MuestraUf,
                    UfRate = UfRate
                };
                vm.OnRecalculateFormulasRequested = RecalculateActiveMaterialFormulas;
                vm.Recalculate();
                vm.SuppressNotifications = false;
                ActiveMateriales.Add(vm);
            }
        }
        finally
        {
            _isLoadingMaterials = false;
        }

        RecalculateActiveMaterialFormulas();
        return true;
    }

    private bool _isRecalculatingFormulas;

    public void RecalculateActiveMaterialFormulas()
    {
        if (_isRecalculatingFormulas || _isLoadingMaterials || SelectedLine == null || SelectedLine.EsTitulo) return;
        _isRecalculatingFormulas = true;
        try
        {
            int matCount = ActiveMateriales.Count;
            if (matCount == 0) return;

            decimal EvaluateCell(int r, int s, HashSet<(int, int)> visiting)
            {
                if (r < 0 || r >= matCount || s < 0 || s >= 24) return 0m;
                if (visiting.Contains((r, s))) return 0m;

                string raw = ActiveMateriales[r].GetSector(s);
                if (string.IsNullOrWhiteSpace(raw)) return 0m;

                string clean = raw.Trim();
                if (clean.StartsWith("=", StringComparison.Ordinal))
                    clean = clean[1..].TrimStart();

                if (string.IsNullOrWhiteSpace(clean)) return 0m;

                // Si es solo un operador mientras el usuario escribe la fórmula (=+, +, -, *), devuelve 0 transitoriamente
                if (clean is "+" or "-" or "*" or "/") return 0m;

                if (ParseDecimal(clean, out var directNum))
                    return directNum;

                var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    ["cant"] = SelectedLine.CantidadAdicional,
                    ["cant_total"] = SelectedLine.CantidadAdicional,
                    ["total"] = SelectedLine.CantidadAdicional,
                    ["cantidad_partida"] = SelectedLine.CantidadAdicional,
                    ["cant_partida"] = SelectedLine.CantidadAdicional
                };

                // Cantidades acumuladas por material para fórmulas que usan CANT1, TOTAL1, etc.
                for (int mIdx = 0; mIdx < matCount; mIdx++)
                {
                    decimal matQty = ActiveMateriales[mIdx].Cantidad;
                    variables[$"CANT{mIdx + 1}"] = matQty;
                    variables[$"TOTAL{mIdx + 1}"] = matQty;
                }

                // Referencias a celdas 2D: F1C1, R1C1, C1F1, C1C1...
                var cellMatches = System.Text.RegularExpressions.Regex.Matches(
                    clean, @"\b(?<p1>[FfRrCc])(?<row>\d+)(?<p2>[CcFf])(?<col>\d+)\b");
                foreach (System.Text.RegularExpressions.Match m in cellMatches)
                {
                    if (int.TryParse(m.Groups["row"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetRow) &&
                        int.TryParse(m.Groups["col"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetCol))
                    {
                        int trIdx = targetRow - 1;
                        int tcIdx = targetCol - 1;
                        visiting.Add((r, s));
                        decimal cellVal = EvaluateCell(trIdx, tcIdx, visiting);
                        visiting.Remove((r, s));
                        variables[m.Value] = cellVal;
                    }
                }

                // Referencias a filas en la misma zona: R1, R2, F1, F2, C1, C2, U1, U2...
                var rowMatches = System.Text.RegularExpressions.Regex.Matches(
                    clean, @"\b(?<prefix>[FfRrCcUu])(?<row>\d+)\b");
                foreach (System.Text.RegularExpressions.Match m in rowMatches)
                {
                    if (variables.ContainsKey(m.Value)) continue;

                    if (int.TryParse(m.Groups["row"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetRow))
                    {
                        int trIdx = targetRow - 1;
                        visiting.Add((r, s));
                        decimal rowVal = EvaluateCell(trIdx, s, visiting);
                        visiting.Remove((r, s));
                        variables[m.Value] = rowVal;
                        variables[$"F{targetRow}"] = rowVal;
                        variables[$"R{targetRow}"] = rowVal;
                        variables[$"C{targetRow}"] = rowVal;
                        variables[$"U{targetRow}"] = rowVal;
                    }
                }

                try
                {
                    var evalResult = QuantityFormulaService.Evaluate(clean, variables);
                    if (evalResult.Success)
                    {
                        return evalResult.Value;
                    }
                }
                catch { }

                return 0m;
            }

            for (int r = 0; r < matCount; r++)
            {
                var mat = ActiveMateriales[r];
                mat.SuppressNotifications = true;
                decimal sumZones = 0m;
                bool hasAnyZone = false;

                for (int s = 0; s < 24; s++)
                {
                    string raw = mat.GetSector(s);
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        mat.SetSectorDisplay(s, "");
                        continue;
                    }

                    hasAnyZone = true;
                    decimal cellVal = EvaluateCell(r, s, new HashSet<(int, int)>());
                    mat.SetSectorDisplay(s, ChangeOrderMaterialVm.FormatDisplayDecimal(cellVal));
                    sumZones += cellVal;
                }

                if (hasAnyZone)
                {
                    mat.WasSetFromZones = true;
                    mat.Cantidad = Math.Max(0m, sumZones);
                    mat.CantidadFormula = mat.Cantidad > 0m ? mat.Cantidad.ToString("G29", CultureInfo.InvariantCulture) : "";
                }
                else if (mat.WasSetFromZones)
                {
                    mat.Cantidad = 0m;
                    mat.CantidadFormula = "";
                    mat.WasSetFromZones = false;
                }
                else if (!string.IsNullOrWhiteSpace(mat.CantidadFormula))
                {
                    mat.EvaluateFormula();
                }
                else if (mat.Cantidad == 0m)
                {
                    mat.CantidadFormula = "";
                }

                mat.Recalculate();
                mat.SuppressNotifications = false;
            }

            SelectedLine.SetMateriales(ActiveMateriales.Select(m => m.ToModel(SelectedLine.ItemId)));
            if (!SelectedLine.EsContractual && !SelectedLine.EsTitulo && SelectedLine.Materiales.Count > 0)
            {
                SelectedLine.UpdateExtraordinariaPriceFromMateriales();
            }
            SelectedLine.Recalculate();
            RecalculateTotals();
            RefreshSelectedLineHeader();
        }
        finally
        {
            _isRecalculatingFormulas = false;
        }
    }

    public void LoadFrom(ChangeOrderRow oc)
    {
        if (oc.GastosGeneralesPct > 0m || oc.UtilidadPct > 0m)
        {
            GastosGeneralesPct = oc.GastosGeneralesPct;
            UtilidadPct = oc.UtilidadPct;
        }

        Numero = oc.Numero;
        Titulo = oc.Titulo;
        CreadoPor = oc.CreadoPor;
        CostoBaseText = ToText(oc.CostoBaseUf);
        Nota = oc.Nota;

        Lineas.Clear();
        if (oc.Lineas != null)
        {
            foreach (var l in oc.Lineas)
            {
                Lineas.Add(ChangeOrderLineVm.FromModel(l, MuestraUf, UfRate, RecalculateTotals));
            }
        }

        if (Lineas.Count > 0)
        {
            SelectedLine = Lineas[0];
            RecalculateTotals();
        }
        else
        {
            SelectedLine = null;
            ActiveMateriales.Clear();
            DeltaVentaText = ToText(oc.DeltaVentaUf);
            DeltaCostoText = ToText(oc.DeltaCostoUf);
        }

        ValidationErrorMessage = null;
        CaptureInitialSnapshot();
    }

    public void AddContractualPartida(ChangeOrderPartidaPickerItem item, IEnumerable<CubicacionRow>? baseProjectCubicacion = null)
    {
        // Buscar si ya está agregada para no duplicar
        var existing = Lineas.FirstOrDefault(l => string.Equals(l.ItemId, item.Id, StringComparison.OrdinalIgnoreCase) ||
                                                 string.Equals(l.Item, item.Item, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.CantidadAdicional += 1m;
            SelectedLine = existing;
            RecalculateTotals();
            return;
        }

        var line = new ChangeOrderLineVm(RecalculateTotals)
        {
            MuestraUf = MuestraUf,
            UfRate = UfRate,
            ItemId = item.Id,
            Item = item.Item,
            DescripcionPartida = item.Descripcion,
            Unidad = string.IsNullOrWhiteSpace(item.Unidad) ? "UN" : item.Unidad,
            EsContractual = true,
            CantidadBase = item.CantidadContrato,
            CantidadAdicional = 1m,
            PrecioUnitarioClp = item.PrecioUnitarioClp,
            PrecioUnitarioUf = item.PrecioUnitarioUf
        };

        // Pre-cargar automáticamente los materiales de esta partida en contrato con zonas y cantidades limpias en 0
        // para que el usuario pueda cubicar desde cero manteniendo los códigos, descripciones y precios contractuales
        if (baseProjectCubicacion != null)
        {
            var baseRows = baseProjectCubicacion
                .Where(c => string.Equals(c.ItemId, item.Id, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(c.Codigo))
                .ToList();

            if (baseRows.Count > 0)
            {
                var mats = new List<CubicacionRow>();
                foreach (var bRow in baseRows)
                {
                    var cRow = new CubicacionRow
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ItemId = item.Id,
                        Tipo = string.IsNullOrWhiteSpace(bRow.Tipo) ? TipoRecurso.MAT : bRow.Tipo,
                        Codigo = bRow.Codigo,
                        Descripcion = bRow.Descripcion,
                        Unidad = string.IsNullOrWhiteSpace(bRow.Unidad) ? "UN" : bRow.Unidad,
                        Cantidad = 0m,
                        TotalExpression = "",
                        QtyExpressions = Array.Empty<string>(),
                        MatClp = bRow.MatClpConFallback,
                        MoClp = bRow.MoClpConFallback,
                        GgUtFactor = bRow.GgUtFactor > 0m ? bRow.GgUtFactor : 1.00m,
                        NotaInterna = bRow.NotaInterna ?? ""
                    };

                    cRow.RecalcularUf(UfRate);
                    mats.Add(cRow);
                }
                line.SetMateriales(mats);
            }
        }

        Lineas.Add(line);
        SelectedLine = line;
        RecalculateTotals();
    }

    public void AddExtraordinariaPartida()
    {
        int nextNum = Lineas.Count(l => !l.EsContractual) + 1;
        var line = new ChangeOrderLineVm(RecalculateTotals)
        {
            MuestraUf = MuestraUf,
            UfRate = UfRate,
            ItemId = $"EXT-{nextNum:D2}",
            Item = $"EXT-{nextNum:D2}",
            DescripcionPartida = "Nueva partida extraordinaria no contemplada en contrato",
            Unidad = "GL",
            EsContractual = false,
            EsTitulo = false,
            CantidadBase = 0m,
            CantidadAdicional = 1m,
            PrecioUnitarioClp = 0m,
            PrecioUnitarioUf = 0m
        };

        Lineas.Add(line);
        SelectedLine = line;
        RecalculateTotals();
    }

    public void AddTitulo() => AddTituloLine();

    public void AddTituloLine()
    {
        int nextNum = Lineas.Count + 1;
        var line = new ChangeOrderLineVm(RecalculateTotals)
        {
            MuestraUf = MuestraUf,
            UfRate = UfRate,
            ItemId = $"TIT-{nextNum:D2}",
            Item = $"TITULO {nextNum}",
            DescripcionPartida = "SECCION O TITULO DE PARTIDAS",
            Unidad = "",
            EsContractual = false,
            EsTitulo = true,
            CantidadBase = 0m,
            CantidadAdicional = 0m,
            PrecioUnitarioClp = 0m,
            PrecioUnitarioUf = 0m
        };

        Lineas.Add(line);
        SelectedLine = line;
        RecalculateTotals();
    }

    public void RemoveLine(ChangeOrderLineVm? line) => DeleteLine(line);

    public void DeleteLine(ChangeOrderLineVm? line)
    {
        if (line == null) return;
        int idx = Lineas.IndexOf(line);
        if (idx < 0) return;

        Lineas.Remove(line);

        if (SelectedLine == line)
        {
            if (Lineas.Count > 0)
            {
                int newIdx = Math.Min(idx, Lineas.Count - 1);
                SelectedLine = Lineas[newIdx];
            }
            else
            {
                SelectedLine = null;
                ActiveMateriales.Clear();
            }

            RecalculateTotals();
        }
    }

    public void RecalculateTotals()
    {
        OnPropertyChanged(nameof(SubtotalContractualClp));
        OnPropertyChanged(nameof(SubtotalContractualUf));
        OnPropertyChanged(nameof(SubtotalExtraordinarioClp));
        OnPropertyChanged(nameof(SubtotalExtraordinarioUf));
        OnPropertyChanged(nameof(TotalAdicionalClp));
        OnPropertyChanged(nameof(TotalAdicionalUf));
        OnPropertyChanged(nameof(SubtotalContractualDisplay));
        OnPropertyChanged(nameof(SubtotalExtraordinarioDisplay));
        OnPropertyChanged(nameof(TotalAdicionalDisplay));
        OnPropertyChanged(nameof(HasLineas));
        OnPropertyChanged(nameof(PartidasCount));
        OnPropertyChanged(nameof(PartidasCountBadgeText));

        if (Lineas.Count > 0)
        {
            decimal deltaCosto = TotalAdicionalUf;
            decimal deltaVenta = Math.Round(deltaCosto * (1m + (GastosGeneralesPct + UtilidadPct) / 100m), 4);
            DeltaCostoText = ToText(deltaCosto);
            DeltaVentaText = ToText(deltaVenta);
        }

        RefreshSelectedLineHeader();
    }

    private string ToText(decimal uf)
        => MuestraUf
            ? uf.ToString("N3", CultureInfo.InvariantCulture)
            : Math.Round(uf * UfRate, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);

    private decimal ToUf(decimal valor)
        => MuestraUf ? valor : PriceConversion.ClpToUf(valor, UfRate);

    private string _initialSnapshot = "";

    public void CaptureInitialSnapshot()
    {
        _initialSnapshot = ComputeSnapshot();
    }

    public bool HasChanges()
    {
        if (SelectedLine != null && !SelectedLine.EsTitulo)
        {
            SelectedLine.SetMateriales(ActiveMateriales.Select(m => m.ToModel(SelectedLine.ItemId)));
        }

        return !string.Equals(_initialSnapshot, ComputeSnapshot(), StringComparison.Ordinal);
    }

    private string ComputeSnapshot()
    {
        var snapshot = new
        {
            Numero = Numero.Trim(),
            Titulo = Titulo.Trim(),
            CreadoPor = CreadoPor.Trim(),
            Nota = Nota.Trim(),
            Lineas = Lineas.Select(l => new
            {
                l.ItemId,
                l.Item,
                l.DescripcionPartida,
                l.Unidad,
                l.EsContractual,
                l.EsTitulo,
                l.CantidadAdicional,
                l.PrecioUnitarioClp,
                l.Nota,
                Materiales = (l.Materiales ?? Enumerable.Empty<CubicacionRow>()).Select(m => new
                {
                    m.Codigo,
                    m.Descripcion,
                    m.Unidad,
                    m.Cantidad,
                    m.MatClp,
                    m.MoClp,
                    m.GgUtFactor,
                    m.NotaInterna,
                    Sectores = m.QtyExpressions ?? Array.Empty<string>()
                }).ToList()
            }).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }

    public bool TrySave(ChangeOrderRow target)
    {
        // Guardar materiales de la linea actualmente seleccionada
        if (SelectedLine != null && !SelectedLine.EsTitulo)
        {
            SelectedLine.SetMateriales(ActiveMateriales.Select(m => m.ToModel(SelectedLine.ItemId)));
        }

        var numero = Numero.Trim();
        var titulo = Titulo.Trim();

        if (string.IsNullOrWhiteSpace(numero))
        {
            ValidationErrorMessage = "El número de orden o adicional es obligatorio.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(titulo))
        {
            ValidationErrorMessage = "El título es obligatorio.";
            return false;
        }

        decimal deltaVenta;
        decimal deltaCosto;
        decimal deltaGg;
        decimal deltaUtil;

        if (Lineas.Count > 0)
        {
            deltaCosto = TotalAdicionalUf;
            deltaGg = Math.Round(deltaCosto * (GastosGeneralesPct / 100m), 4);
            deltaUtil = Math.Round(deltaCosto * (UtilidadPct / 100m), 4);
            deltaVenta = Math.Round(deltaCosto * (1m + (GastosGeneralesPct + UtilidadPct) / 100m), 4);
        }
        else
        {
            if (!ParseDecimal(DeltaVentaText, out var dv))
            {
                ValidationErrorMessage = $"Delta venta inválido ({EtiquetaMoneda}).";
                return false;
            }
            if (!ParseDecimal(DeltaCostoText, out var dc))
            {
                ValidationErrorMessage = $"Delta costo inválido ({EtiquetaMoneda}).";
                return false;
            }
            deltaVenta = ToUf(dv);
            deltaCosto = ToUf(dc);
            deltaGg = Math.Round(deltaCosto * (GastosGeneralesPct / 100m), 4);
            deltaUtil = Math.Round(deltaCosto * (UtilidadPct / 100m), 4);
        }

        decimal costoBase = 0m;
        if (!string.IsNullOrWhiteSpace(CostoBaseText))
        {
            if (!ParseDecimal(CostoBaseText, out costoBase) || costoBase < 0m)
            {
                ValidationErrorMessage = "Costo base: ingrese un número válido no negativo.";
                return false;
            }
        }

        target.Numero = numero;
        target.Titulo = titulo;
        target.CreadoPor = CreadoPor.Trim();
        target.CostoBaseUf = ToUf(costoBase);
        target.Nota = Nota.Trim();
        target.GastosGeneralesPct = GastosGeneralesPct;
        target.UtilidadPct = UtilidadPct;
        target.DeltaCostoUf = deltaCosto;
        target.DeltaGastosGeneralesUf = deltaGg;
        target.DeltaUtilidadUf = deltaUtil;
        target.DeltaVentaUf = deltaVenta;
        target.FechaModificacion = AppClock.Now;

        target.Lineas.Clear();
        foreach (var l in Lineas)
        {
            target.Lineas.Add(l.ToModel());
        }

        return true;
    }

    public static bool ParseDecimal(string? text, out decimal val)
    {
        val = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = text.Trim().Replace("$", "").Replace("UF", "").Trim();
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
            return true;
        var clCulture = new CultureInfo("es-CL");
        if (decimal.TryParse(clean, NumberStyles.Any, clCulture, out val))
            return true;
        return false;
    }

    private void ClearError()
    {
        if (ValidationErrorMessage != null)
            ValidationErrorMessage = null;
    }
}