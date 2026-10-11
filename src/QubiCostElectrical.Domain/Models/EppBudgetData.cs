using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class EppItemRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Concepto { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitarioClp { get; set; }
}

public sealed class EppSeccionRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Titulo { get; set; } = "";
    public string Badge { get; set; } = "";
    public List<EppItemRow> Items { get; set; } = new();
}

/// <summary>
/// Presupuesto EPP del proyecto: indumentaria MO directa/indirecta + accesorios opcionales.
/// </summary>
public sealed class EppBudgetData
{
    /// <summary>Duración de la obra en meses (Excel MANO DE OBRA!B17).</summary>
    public int MesesObra { get; set; }
    /// <summary>Ciclo de reposición de la MO directa. En el Excel es 3.</summary>
    public int MesesDirecta { get; set; } = 3;
    /// <summary>Ciclo de reposición de la MO indirecta. En el Excel es 6.</summary>
    public int MesesIndirecta { get; set; } = 6;
    /// <summary>Promedio mensual de trabajadores directos, redondeado hacia arriba (Excel Q46).</summary>
    public int DotacionDirecta { get; set; }
    /// <summary>Dotación indirecta usada en el EPP. En el Excel de referencia es 5.</summary>
    public int DotacionIndirecta { get; set; }
    public bool OpcionalesIncluidos { get; set; } = true;
    public List<EppSeccionRow> MoDirecta { get; set; } = new();
    public List<EppSeccionRow> MoIndirecta { get; set; } = new();
    public List<EppSeccionRow> AccesoriosOpcionales { get; set; } = new();

    public bool HasContent =>
        MoDirecta.Count > 0 || MoIndirecta.Count > 0 || AccesoriosOpcionales.Count > 0;

    public static EppBudgetData Default() => new();

    /// <summary>Plantilla EPP limpia; cantidades derivadas de la dotación MO (0 si no hay gente).</summary>
    public static EppBudgetData CreateSeed(int mesesObra = 0, int promedioDirecto = 0, int dotacionIndirecta = 0)
    {
        var data = new EppBudgetData
        {
            MesesObra = Math.Max(0, mesesObra),
            MesesDirecta = 3,
            MesesIndirecta = 6,
            DotacionDirecta = Math.Max(0, promedioDirecto),
            DotacionIndirecta = Math.Max(0, dotacionIndirecta),
            OpcionalesIncluidos = true
        };

        // Precios de plantilla; cantidades se aplican con SyncQuantitiesFromDotacion.
        data.MoDirecta.Add(Seccion("I — Vestimenta", "3 meses",
            ("Zapatos de seguridad", 0, 6900m),
            ("Pantalones Jeans", 0, 5050m),
            ("Polera", 0, 2800m),
            ("Chaquetilla o Geólogo", 0, 4490m),
            ("Casco Proseg Azul", 0, 1180m),
            ("Barbiquejo", 0, 180m)));
        data.MoDirecta.Add(Seccion("II — Accesorios", "3 meses",
            ("Antiparras Claras", 0, 1980m),
            ("Protectores Auditivo (Tapón)", 0, 170m),
            ("Guantes Cabritilla", 0, 990m),
            ("Mascarilla Desechable", 0, 720m)));

        data.MoIndirecta.Add(Seccion("I — Vestimenta", "6 meses",
            ("Zapatos de seguridad", 0, 30000m),
            ("Pantalones Jeans", 0, 5050m),
            ("Polera", 0, 2800m),
            ("Chaquetilla o Geólogo", 0, 4490m),
            ("Casco Blanco", 0, 9200m),
            ("Barbiquejo", 0, 180m)));
        data.MoIndirecta.Add(Seccion("II — Accesorios", "6 meses",
            ("Antiparras Claras", 0, 1980m),
            ("Protectores Auditivo (Tapón)", 0, 170m),
            ("Guantes Cabritilla", 0, 990m),
            ("Mascarilla Desechable", 0, 720m)));

        data.AccesoriosOpcionales.Add(Seccion("III — Elementos Basicos", "Costo Fijo",
            ("Protectores Auditivo (Fono)", 0, 3690m),
            ("Cubrenuca", 0, 590m),
            ("Mascarilla \"Masprot P100\"", 0, 4200m),
            ("Filtros P100", 0, 1790m),
            ("Arnés", 0, 7400m),
            ("Cabos de Vida", 0, 3500m),
            ("Bloqueador Solar", 0, 7590m)));

        data.SyncQuantitiesFromDotacion();
        return data;
    }

    /// <summary>
    /// Vestimenta y accesorios de MO directa: (meses de obra / ciclo) × promedio de trabajadores.
    /// Excel: (B17/3)*Q46.
    /// </summary>
    public static decimal ComputeCantidadDirecta(int mesesObra, int cicloMeses, int promedioDirecto)
    {
        if (mesesObra <= 0 || cicloMeses <= 0 || promedioDirecto <= 0)
            return 0m;
        return mesesObra / (decimal)cicloMeses * promedioDirecto;
    }

    /// <summary>
    /// Vestimenta y accesorios de MO indirecta: (meses de obra / ciclo) × dotación indirecta.
    /// Excel: (B17/6)*5.
    /// </summary>
    public static decimal ComputeCantidadIndirecta(int mesesObra, int cicloMeses, int dotacionIndirecta)
    {
        if (mesesObra <= 0 || dotacionIndirecta <= 0)
            return 0m;
        var ciclo = cicloMeses > 0 ? cicloMeses : 6;
        return mesesObra / (decimal)ciclo * dotacionIndirecta;
    }

    /// <summary>Accesorios opcionales, cada uno con la fórmula de la hoja EPP.</summary>
    public static decimal ComputeCantidadOpcional(string? concepto, int mesesObra, int cicloDirecta, int cicloIndirecta, int promedioDirecto)
    {
        if (mesesObra <= 0 && promedioDirecto <= 0)
            return 0m;
        var name = concepto ?? "";
        if (name.Contains("Cubrenuca", StringComparison.OrdinalIgnoreCase))
            return ComputeCantidadDirecta(mesesObra, cicloDirecta, promedioDirecto);
        if (name.Contains("Bloqueador", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Solar", StringComparison.OrdinalIgnoreCase))
            return mesesObra / 2m;
        if (name.Contains("Arnés", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Arnes", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Cabo", StringComparison.OrdinalIgnoreCase))
            return promedioDirecto * 2m;
        if (name.Contains("Fono", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Masprot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Filtro", StringComparison.OrdinalIgnoreCase))
            return ComputeCantidadIndirecta(mesesObra, cicloIndirecta, promedioDirecto);
        return ComputeCantidadDirecta(mesesObra, cicloDirecta, promedioDirecto);
    }

    public readonly record struct EppMoDrivers(int MesesObra, int PromedioDirecto, int DotacionIndirecta);

    /// <summary>
    /// Lee los tres datos que usa la hoja EPP: meses de obra, promedio de trabajadores directos
    /// y dotación indirecta.
    /// </summary>
    public static EppMoDrivers ReadMoDrivers(ProjectFileData? data)
    {
        if (data == null)
            return new EppMoDrivers(0, 0, 0);

        int meses = data.Proyecto?.MoDuracionMeses ?? 0;
        if (meses <= 0)
            meses = ReadMoMeses(data);

        int promedio = RoundUpAverage(MonthlyTotals(
            data.MoDirecta?.Select(r => (Math.Max(0, r.Dotacion), r.DotacionMensual)),
            meses));
        if (promedio <= 0)
            promedio = data.MoDirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;

        int indirecta = data.MoIndirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;
        if (indirecta <= 0)
            indirecta = RoundUpAverage(MonthlyTotals(
                data.MoIndirecta?.Select(r => (Math.Max(0, r.Dotacion), r.DotacionMensual)),
                meses));

        return new EppMoDrivers(Math.Max(0, meses), Math.Max(0, promedio), Math.Max(0, indirecta));
    }

    private static IReadOnlyList<int> MonthlyTotals(IEnumerable<(int Dotacion, List<int> Mensual)>? rows, int meses)
    {
        var list = rows?.ToList() ?? new List<(int, List<int>?)>();
        if (meses <= 0 || list.Count == 0 || list.All(r => r.Mensual is not { Count: > 0 }))
            return Array.Empty<int>();

        var totals = new int[meses];
        for (var i = 0; i < meses; i++)
        {
            var head = 0;
            foreach (var row in list)
            {
                if (row.Mensual != null && i < row.Mensual.Count)
                    head += Math.Max(0, row.Mensual[i]);
            }
            totals[i] = head;
        }
        return totals;
    }

    private static int RoundUpAverage(IReadOnlyList<int> months)
    {
        if (months.Count == 0)
            return 0;
        var avg = months.Sum(x => (decimal)x) / months.Count;
        if (avg <= 0m)
            return 0;
        return (int)Math.Ceiling(avg);
    }

    public static int ReadMoMeses(ProjectFileData? data)
    {
        if (data == null)
            return 0;
        var m = data.MoDirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 0;
        if (m <= 0)
            m = data.MoIndirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 0;
        return m;
    }

    /// <summary>Actualiza dotaciones y recalcula cantidades de ítems desde MO.</summary>
    public void ApplyMoDotacion(int mesesObra, int promedioDirecto, int dotacionIndirecta)
    {
        MesesObra = Math.Max(0, mesesObra);
        DotacionDirecta = Math.Max(0, promedioDirecto);
        DotacionIndirecta = Math.Max(0, dotacionIndirecta);
        SyncQuantitiesFromDotacion();
    }

    public void SyncQuantitiesFromDotacion()
    {
        var cantDir = ComputeCantidadDirecta(MesesObra, MesesDirecta, DotacionDirecta);
        var cantInd = ComputeCantidadIndirecta(MesesObra, MesesIndirecta, DotacionIndirecta);

        foreach (var item in MoDirecta.SelectMany(s => s.Items))
            item.Cantidad = cantDir;
        foreach (var item in MoIndirecta.SelectMany(s => s.Items))
            item.Cantidad = cantInd;

        foreach (var item in AccesoriosOpcionales.SelectMany(s => s.Items))
            item.Cantidad = ComputeCantidadOpcional(item.Concepto, MesesObra, MesesDirecta, MesesIndirecta, DotacionDirecta);
    }

    /// <summary>Líneas resumen para GO categoría 3.5 (Costos por EPP).</summary>
    public List<GastoOperacionalItemRow> ToGoCategoryItems()
    {
        var items = new List<GastoOperacionalItemRow>
        {
            new()
            {
                Concepto = "EPP indumentaria MO directa",
                Unidad = "GL",
                Cantidad = TotalDirecta > 0 ? 1 : 0,
                PrecioUnitarioClp = TotalDirecta
            },
            new()
            {
                Concepto = "EPP indumentaria MO indirecta",
                Unidad = "GL",
                Cantidad = TotalIndirecta > 0 ? 1 : 0,
                PrecioUnitarioClp = TotalIndirecta
            },
            new()
            {
                Concepto = OpcionalesIncluidos
                    ? "EPP accesorios opcionales"
                    : "EPP accesorios opcionales (excluido)",
                Unidad = "GL",
                Cantidad = OpcionalesIncluidos && TotalOpcionales > 0 ? 1 : 0,
                PrecioUnitarioClp = OpcionalesIncluidos ? TotalOpcionales : 0
            }
        };
        return items;
    }

    /// <summary>
    /// Sincroniza EPP desde MO y empuja el total a Gastos Operacionales 3.5.
    /// Garantiza plantilla EPP si el proyecto aún no tiene contenido.
    /// </summary>
    public static bool SyncEppAndGoFromMo(ProjectFileData data)
    {
        if (data == null)
            return false;

        var mo = ReadMoDrivers(data);
        var epp = data.Epp;
        if (epp == null || !epp.HasContent)
        {
            epp = CreateSeed(mo.MesesObra, mo.PromedioDirecto, mo.DotacionIndirecta);
            data.Epp = epp;
        }
        else
        {
            epp.ApplyMoDotacion(mo.MesesObra, mo.PromedioDirecto, mo.DotacionIndirecta);
        }

        return PushToGoCategory(data, epp);
    }

    /// <summary>Escribe/actualiza la categoría GO 3.5 desde el presupuesto EPP.</summary>
    public static bool PushToGoCategory(ProjectFileData data, EppBudgetData? epp = null)
    {
        epp ??= data.Epp;
        if (data == null || epp == null)
            return false;

        // Nunca dejar el GO solo con 3.5: asegurar plantilla 3.1–3.6 primero.
        GastoOperacionalPlantilla.EnsureComplete(data);

        var cat = data.GastosOperacionales!.FirstOrDefault(c =>
            string.Equals(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase)
            || c.Concepto.Contains("EPP", StringComparison.OrdinalIgnoreCase));

        var goItems = epp.ToGoCategoryItems();
        var monto = goItems.Sum(i => Math.Round(i.Cantidad * i.PrecioUnitarioClp));

        if (cat == null)
        {
            cat = new GastoOperacionalRow
            {
                Codigo = "3.5",
                Concepto = "Costos por EPP",
                Items = goItems,
                MontoClp = monto
            };
            var idx = data.GastosOperacionales.FindIndex(c =>
                string.Compare(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase) > 0);
            if (idx < 0)
                data.GastosOperacionales.Add(cat);
            else
                data.GastosOperacionales.Insert(idx, cat);
            return true;
        }

        cat.Concepto = string.IsNullOrWhiteSpace(cat.Concepto) ? "Costos por EPP" : cat.Concepto;
        cat.Items = goItems;
        cat.MontoClp = monto;
        return true;
    }

    public static IReadOnlyList<(string Concepto, decimal Precio, string Codigo)> Catalog { get; } =
        new List<(string, decimal, string)>
        {
            ("Zapatos de seguridad", 6900m, "EPP-ZAPAT-S"),
            ("Pantalones Jeans", 5050m, "EPP-PANT-J"),
            ("Polera", 2800m, "EPP-POL-B"),
            ("Chaquetilla o Geólogo", 4490m, "EPP-CHAL-G"),
            ("Casco Proseg Azul", 1180m, "EPP-CASCO-A"),
            ("Casco Blanco", 9200m, "EPP-CASCO-B"),
            ("Barbiquejo", 180m, "EPP-BARB"),
            ("Antiparras Claras", 1980m, "EPP-LENTE-C"),
            ("Protectores Auditivo (Tapón)", 170m, "EPP-TAPA-O"),
            ("Protectores Auditivo (Fono)", 3690m, "EPP-FONO"),
            ("Guantes Cabritilla", 990m, "EPP-GUANTE-C"),
            ("Mascarilla Desechable", 720m, "EPP-MASC-D"),
            ("Mascarilla \"Masprot P100\"", 4200m, "EPP-MASC-95"),
            ("Filtros P100", 1790m, "EPP-FILT-P"),
            ("Arnés", 7400m, "EPP-ARNES-4"),
            ("Cabos de Vida", 3500m, "EPP-CABO"),
            ("Cubrenuca", 590m, "EPP-NUCA"),
            ("Bloqueador Solar", 7590m, "EPP-SOLAR"),
        };

    private static EppSeccionRow Seccion(string titulo, string badge, params (string Concepto, decimal Cant, decimal Precio)[] items)
    {
        var sec = new EppSeccionRow { Titulo = titulo, Badge = badge };
        foreach (var (concepto, cant, precio) in items)
        {
            sec.Items.Add(new EppItemRow
            {
                Concepto = concepto,
                Cantidad = cant,
                PrecioUnitarioClp = precio
            });
        }
        return sec;
    }

    public decimal TotalDirecta => MoDirecta.Sum(s => s.Items.Sum(i => Math.Round(i.Cantidad * i.PrecioUnitarioClp)));
    public decimal TotalIndirecta => MoIndirecta.Sum(s => s.Items.Sum(i => Math.Round(i.Cantidad * i.PrecioUnitarioClp)));
    public decimal TotalOpcionales => AccesoriosOpcionales.Sum(s => s.Items.Sum(i => Math.Round(i.Cantidad * i.PrecioUnitarioClp)));
    public decimal GranTotal => TotalDirecta + TotalIndirecta + (OpcionalesIncluidos ? TotalOpcionales : 0m);
}
