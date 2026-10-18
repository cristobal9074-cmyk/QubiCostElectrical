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

    public string EtiquetaMoneda => MuestraUf ? "UF" : "$";

    public string CostoBaseLabel => $"Costo base ({EtiquetaMoneda})";
    public string DeltaCostoLabel => $"Delta costo ({EtiquetaMoneda})";
    public string DeltaVentaLabel => $"Delta venta ({EtiquetaMoneda})";

    public decimal SubtotalContractualClp => Lineas.Where(l => l.EsContractual).Sum(l => l.SubtotalClp);
    public decimal SubtotalContractualUf => Lineas.Where(l => l.EsContractual).Sum(l => l.SubtotalUf);
    public decimal SubtotalExtraordinarioClp => Lineas.Where(l => !l.EsContractual).Sum(l => l.SubtotalClp);
    public decimal SubtotalExtraordinarioUf => Lineas.Where(l => !l.EsContractual).Sum(l => l.SubtotalUf);

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

    partial void OnNumeroChanged(string value) => ClearError();
    partial void OnTituloChanged(string value) => ClearError();
    partial void OnCreadoPorChanged(string value) => ClearError();
    partial void OnNotaChanged(string value) => ClearError();

    partial void OnSelectedLineChanged(ChangeOrderLineVm? oldValue, ChangeOrderLineVm? newValue)
    {
        // Guardar materiales de la partida anterior
        if (oldValue != null)
        {
            oldValue.SetMateriales(ActiveMateriales.Select(m => m.ToModel(oldValue.ItemId)));
        }

        ActiveMateriales.Clear();
        if (newValue != null && newValue.Materiales != null)
        {
            foreach (var mat in newValue.Materiales)
            {
                ActiveMateriales.Add(ChangeOrderMaterialVm.FromModel(mat, MuestraUf, UfRate, OnActiveMaterialChanged));
            }
        }

        RefreshSelectedLineHeader();
    }

    public void RefreshSelectedLineHeader()
    {
        OnPropertyChanged(nameof(HasSelectedLine));
        OnPropertyChanged(nameof(SelectedLineItemText));
        OnPropertyChanged(nameof(SelectedLineDescText));
        OnPropertyChanged(nameof(SelectedLinePuLabel));
        OnPropertyChanged(nameof(SelectedLinePuDisplay));
        OnPropertyChanged(nameof(SelectedLineSubtotalDisplay));
        OnPropertyChanged(nameof(SelectedLineCantTotalDisplay));
    }

    private void OnActiveMaterialChanged()
    {
        if (SelectedLine == null) return;

        // Persistir en SelectedLine.Materiales
        SelectedLine.SetMateriales(ActiveMateriales.Select(m => m.ToModel(SelectedLine.ItemId)));

        // Si la partida tiene 1 material o aún no ha sido cubicada (<= 1), sincronizar con la medición de materiales
        // Salvo que la unidad sea "GL" (Global), cuya cantidad de partida es fija (1 GL) y su P.U. es la suma de los materiales
        if (!string.Equals(SelectedLine.Unidad, "GL", StringComparison.OrdinalIgnoreCase))
        {
            if (ActiveMateriales.Count == 1 || SelectedLine.CantidadAdicional <= 1m)
            {
                decimal maxZoneQty = ActiveMateriales.Count > 0 ? ActiveMateriales.Max(m => m.Cantidad) : 0m;
                if (maxZoneQty > 0m && Math.Abs(SelectedLine.CantidadAdicional - maxZoneQty) > 0.001m)
                {
                    SelectedLine.CantidadAdicional = maxZoneQty;
                }
            }
        }

        // Si es partida EXTRAORDINARIA, el precio unitario y subtotal se calculan
        // automáticamente a partir de la cubicación de sus recursos y materiales
        if (!SelectedLine.EsContractual && ActiveMateriales.Count > 0)
        {
            decimal totalMatClp = ActiveMateriales.Sum(m => m.SubtotalClp);
            decimal totalMatUf = ActiveMateriales.Sum(m => m.SubtotalUf);

            if (totalMatClp > 0m || totalMatUf > 0m)
            {
                if (SelectedLine.CantidadAdicional > 0m)
                {
                    SelectedLine.PrecioUnitarioClp = Math.Round(totalMatClp / SelectedLine.CantidadAdicional, 0, MidpointRounding.AwayFromZero);
                    SelectedLine.PrecioUnitarioUf = SelectedLine.UfRate > 0m
                        ? PriceConversion.ClpToUf(SelectedLine.PrecioUnitarioClp, SelectedLine.UfRate)
                        : (totalMatUf / SelectedLine.CantidadAdicional);
                }
                else
                {
                    SelectedLine.PrecioUnitarioClp = totalMatClp;
                    SelectedLine.PrecioUnitarioUf = totalMatUf;
                }
                SelectedLine.Recalculate();
            }
        }

        RecalculateTotals();
        RefreshSelectedLineHeader();
    }

    public void AddMaterialToActiveLine(string codigo = "", string desc = "", string ud = "UN")
    {
        if (SelectedLine == null) return;

        var vm = new ChangeOrderMaterialVm(OnActiveMaterialChanged)
        {
            Tipo = TipoRecurso.MAT,
            Codigo = codigo,
            Descripcion = desc,
            Unidad = string.IsNullOrWhiteSpace(ud) ? "UN" : ud,
            Cantidad = 1m,
            Zona1 = "1",
            MuestraUf = MuestraUf,
            UfRate = UfRate
        };
        vm.Recalculate();
        ActiveMateriales.Add(vm);
        OnActiveMaterialChanged();
    }

    public void RemoveMaterialFromActiveLine(ChangeOrderMaterialVm? mat)
    {
        if (mat != null && ActiveMateriales.Contains(mat))
        {
            ActiveMateriales.Remove(mat);
            OnActiveMaterialChanged();
        }
    }

    public bool PreloadBaseMaterialsForActiveLine(IEnumerable<CubicacionRow>? baseProjectCubicacion)
    {
        if (SelectedLine == null || string.IsNullOrWhiteSpace(SelectedLine.ItemId) || baseProjectCubicacion == null)
            return false;

        var baseRows = baseProjectCubicacion
            .Where(c => string.Equals(c.ItemId, SelectedLine.ItemId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(c.Codigo))
            .ToList();

        if (baseRows.Count == 0) return false;

        decimal ratio = SelectedLine.CantidadBase > 0m
            ? (SelectedLine.CantidadAdicional / SelectedLine.CantidadBase)
            : 1m;

        ActiveMateriales.Clear();
        foreach (var bRow in baseRows)
        {
            decimal cantEscalada = Math.Round(bRow.Cantidad * ratio, 4);
            if (cantEscalada <= 0m && bRow.Cantidad > 0m) cantEscalada = bRow.Cantidad;

            var vm = new ChangeOrderMaterialVm(OnActiveMaterialChanged)
            {
                Tipo = string.IsNullOrWhiteSpace(bRow.Tipo) ? TipoRecurso.MAT : bRow.Tipo,
                Codigo = bRow.Codigo,
                Descripcion = bRow.Descripcion,
                Unidad = string.IsNullOrWhiteSpace(bRow.Unidad) ? "UN" : bRow.Unidad,
                Cantidad = cantEscalada,
                MatClp = bRow.MatClpConFallback,
                MoClp = bRow.MoClpConFallback,
                MuestraUf = MuestraUf,
                UfRate = UfRate
            };

            if (bRow.QtyExpressions != null && bRow.QtyExpressions.Length > 0)
            {
                for (int q = 0; q < bRow.QtyExpressions.Length; q++)
                {
                    string qExp = bRow.QtyExpressions[q];
                    if (string.IsNullOrWhiteSpace(qExp))
                    {
                        vm.SetSector(q, "");
                    }
                    else if (ratio == 1m)
                    {
                        vm.SetSector(q, qExp);
                    }
                    else if (ParseDecimal(qExp, out var val))
                    {
                        vm.SetSector(q, Math.Round(val * ratio, 4).ToString("G29", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        vm.SetSector(q, qExp);
                    }
                }
            }
            else
            {
                vm.SetSector(0, cantEscalada.ToString("G29", CultureInfo.InvariantCulture));
            }

            vm.Recalculate();
            ActiveMateriales.Add(vm);
        }

        OnActiveMaterialChanged();
        return true;
    }

    public void LoadFrom(ChangeOrderRow oc)
    {
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

        // Pre-cargar automáticamente los materiales de esta partida en contrato
        if (baseProjectCubicacion != null)
        {
            var baseRows = baseProjectCubicacion
                .Where(c => string.Equals(c.ItemId, item.Id, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(c.Codigo))
                .ToList();

            if (baseRows.Count > 0)
            {
                decimal ratio = item.CantidadContrato > 0m ? (1m / item.CantidadContrato) : 1m;
                var mats = new List<CubicacionRow>();
                foreach (var bRow in baseRows)
                {
                    decimal cantEscalada = Math.Round(bRow.Cantidad * ratio, 4);
                    if (cantEscalada <= 0m && bRow.Cantidad > 0m) cantEscalada = bRow.Cantidad;

                    var cRow = new CubicacionRow
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ItemId = item.Id,
                        Tipo = string.IsNullOrWhiteSpace(bRow.Tipo) ? TipoRecurso.MAT : bRow.Tipo,
                        Codigo = bRow.Codigo,
                        Descripcion = bRow.Descripcion,
                        Unidad = string.IsNullOrWhiteSpace(bRow.Unidad) ? "UN" : bRow.Unidad,
                        Cantidad = cantEscalada,
                        TotalExpression = cantEscalada.ToString("G29", CultureInfo.InvariantCulture),
                        MatClp = bRow.MatClpConFallback,
                        MoClp = bRow.MoClpConFallback
                    };

                    if (bRow.QtyExpressions != null && bRow.QtyExpressions.Length > 0)
                    {
                        var qList = new List<string>();
                        for (int q = 0; q < bRow.QtyExpressions.Length; q++)
                        {
                            string qExp = bRow.QtyExpressions[q];
                            if (string.IsNullOrWhiteSpace(qExp))
                                qList.Add("");
                            else if (ratio == 1m)
                                qList.Add(qExp);
                            else if (ParseDecimal(qExp, out var val))
                                qList.Add(Math.Round(val * ratio, 4).ToString("G29", CultureInfo.InvariantCulture));
                            else
                                qList.Add(qExp);
                        }
                        cRow.QtyExpressions = qList.ToArray();
                    }
                    else
                    {
                        cRow.QtyExpressions = new[] { cantEscalada.ToString("G29", CultureInfo.InvariantCulture) };
                    }

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
            CantidadBase = 0m,
            CantidadAdicional = 1m,
            PrecioUnitarioClp = 0m,
            PrecioUnitarioUf = 0m
        };

        Lineas.Add(line);
        SelectedLine = line;
        RecalculateTotals();
    }

    public void RemoveLine(ChangeOrderLineVm? line)
    {
        if (line != null && Lineas.Contains(line))
        {
            int idx = Lineas.IndexOf(line);
            Lineas.Remove(line);

            if (SelectedLine == line)
            {
                SelectedLine = Lineas.Count > 0 ? Lineas[Math.Max(0, idx - 1)] : null;
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

        if (Lineas.Count > 0)
        {
            DeltaVentaText = ToText(TotalAdicionalUf);
            DeltaCostoText = ToText(TotalAdicionalUf);
        }

        RefreshSelectedLineHeader();
    }

    private string ToText(decimal uf)
        => MuestraUf
            ? uf.ToString("N3", CultureInfo.InvariantCulture)
            : Math.Round(uf * UfRate, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);

    private decimal ToUf(decimal valor)
        => MuestraUf ? valor : PriceConversion.ClpToUf(valor, UfRate);

    public bool TrySave(ChangeOrderRow target)
    {
        // Guardar materiales de la línea actualmente seleccionada
        if (SelectedLine != null)
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

        if (Lineas.Count > 0)
        {
            deltaVenta = TotalAdicionalUf;
            deltaCosto = TotalAdicionalUf;
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
        target.DeltaVentaUf = deltaVenta;
        target.DeltaCostoUf = deltaCosto;
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
        return decimal.TryParse(clean, NumberStyles.Any, clCulture, out val);
    }

    private void ClearError() => ValidationErrorMessage = null;
}
