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
    public int MesesDirecta { get; set; } = 3;
    public int MesesIndirecta { get; set; } = 6;
    public int DotacionDirecta { get; set; }
    public int DotacionIndirecta { get; set; }
    public bool OpcionalesIncluidos { get; set; } = true;
    public List<EppSeccionRow> MoDirecta { get; set; } = new();
    public List<EppSeccionRow> MoIndirecta { get; set; } = new();
    public List<EppSeccionRow> AccesoriosOpcionales { get; set; } = new();

    public bool HasContent =>
        MoDirecta.Count > 0 || MoIndirecta.Count > 0 || AccesoriosOpcionales.Count > 0;

    public static EppBudgetData Default() => new();

    /// <summary>Plantilla EPP limpia; cantidades derivadas de la dotación MO (0 si no hay gente).</summary>
    public static EppBudgetData CreateSeed(int dotacionDirecta = 0, int dotacionIndirecta = 0)
    {
        var data = new EppBudgetData
        {
            MesesDirecta = 3,
            MesesIndirecta = 6,
            DotacionDirecta = Math.Max(0, dotacionDirecta),
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

    /// <summary>Cantidad EPP MO directa: dotación / ciclo de reposición (meses).</summary>
    public static decimal ComputeCantidadDirecta(int dotacion, int mesesCiclo)
    {
        if (dotacion <= 0 || mesesCiclo <= 0)
            return 0m;
        return Math.Round(dotacion / (decimal)mesesCiclo, 2);
    }

    /// <summary>Cantidad EPP MO indirecta: roles × (meses/12) × 1.5.</summary>
    public static decimal ComputeCantidadIndirecta(int dotacion, int mesesCiclo)
    {
        if (dotacion <= 0)
            return 0m;
        var meses = mesesCiclo > 0 ? mesesCiclo : 6;
        return Math.Round(dotacion * meses / 12m * 1.5m, 2);
    }

    public static (int Dir, int Ind) ReadMoDotacion(ProjectFileData? data)
    {
        if (data == null)
            return (0, 0);
        var dir = data.MoDirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;
        var ind = data.MoIndirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;
        return (dir, ind);
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
    public void ApplyMoDotacion(int dotacionDirecta, int dotacionIndirecta)
    {
        DotacionDirecta = Math.Max(0, dotacionDirecta);
        DotacionIndirecta = Math.Max(0, dotacionIndirecta);
        SyncQuantitiesFromDotacion();
    }

    public void SyncQuantitiesFromDotacion()
    {
        var cantDir = ComputeCantidadDirecta(DotacionDirecta, MesesDirecta);
        var cantInd = ComputeCantidadIndirecta(DotacionIndirecta, MesesIndirecta);

        foreach (var item in MoDirecta.SelectMany(s => s.Items))
            item.Cantidad = cantDir;
        foreach (var item in MoIndirecta.SelectMany(s => s.Items))
            item.Cantidad = cantInd;

        // Opcionales: se escalan con la dotación directa (Cubrenuca = cantDir;
        // resto proporcional al pico directo cuando hay gente; 0 si no hay MO).
        var hasMo = DotacionDirecta > 0 || DotacionIndirecta > 0;
        foreach (var item in AccesoriosOpcionales.SelectMany(s => s.Items))
        {
            if (item.Concepto.Contains("Cubrenuca", StringComparison.OrdinalIgnoreCase))
            {
                item.Cantidad = cantDir;
                continue;
            }

            if (!hasMo)
            {
                item.Cantidad = 0;
                continue;
            }

            // Heurística estable vs plantilla histórica (154 directa → factores demo).
            var factor = DotacionDirecta > 0 ? DotacionDirecta / 154m : DotacionIndirecta / 6m;
            item.Cantidad = Math.Round(BaseOpcionalCantidad(item.Concepto) * factor, 2);
        }
    }

    private static decimal BaseOpcionalCantidad(string concepto)
    {
        if (concepto.Contains("Fono", StringComparison.OrdinalIgnoreCase)) return 25.67m;
        if (concepto.Contains("Masprot", StringComparison.OrdinalIgnoreCase)) return 26.00m;
        if (concepto.Contains("Filtros", StringComparison.OrdinalIgnoreCase)) return 25.67m;
        if (concepto.Contains("Arnés", StringComparison.OrdinalIgnoreCase)) return 28.00m;
        if (concepto.Contains("Cabos", StringComparison.OrdinalIgnoreCase)) return 28.00m;
        if (concepto.Contains("Bloqueador", StringComparison.OrdinalIgnoreCase)) return 5.50m;
        return 1m;
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

        var (dotDir, dotInd) = ReadMoDotacion(data);
        var epp = data.Epp;
        if (epp == null || !epp.HasContent)
        {
            epp = CreateSeed(dotDir, dotInd);
            data.Epp = epp;
        }
        else
        {
            epp.ApplyMoDotacion(dotDir, dotInd);
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
