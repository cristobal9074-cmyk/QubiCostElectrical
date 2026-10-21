using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Origen del factor de presentación que el motor aplica a una familia.</summary>
public enum OrigenFactorPresentacion
{
    /// <summary>El proyecto tiene un valor propio configurado para la familia.</summary>
    Propio,

    /// <summary>La familia está asignada a una fila canónica y hereda su factor.</summary>
    Asignada,

    /// <summary>La familia hereda por sinonimia canónica el factor de otra familia.</summary>
    Natural,

    /// <summary>Nadie configuró la familia: se usa el valor por defecto del producto.</summary>
    SinConfigurar
}

/// <summary>Estado de una familia de material dentro de un proyecto.</summary>
public sealed class FamiliaPresentacionInfo
{
    public string Nombre { get; set; } = "";
    public string Clave { get; set; } = "";
    public string NombreObjetivo { get; set; } = "";
    public string ClaveObjetivo { get; set; } = "";
    public OrigenFactorPresentacion Origen { get; set; }
    public decimal FactorEfectivo { get; set; }
    public decimal CostoClp { get; set; }
    public decimal SharePct { get; set; }
    public bool UsadaEnCubicacion { get; set; }
    public bool TieneValorPropioIgnorado { get; set; }
    public bool Configurada => Origen != OrigenFactorPresentacion.SinConfigurar;
}

/// <summary>Fila canónica de coeficientes: factor propio y familias que caen en ella.</summary>
public sealed class BucketPresentacionInfo
{
    public string Nombre { get; set; } = "";
    public string Clave { get; set; } = "";
    public string Etiqueta { get; set; } = "";
    public OrigenFactorPresentacion Origen { get; set; }
    public decimal Markup { get; set; }
    public decimal DescuentoPct { get; set; }
    public decimal FactorEfectivo { get; set; }
    public decimal FactorAplicado { get; set; }
    public decimal CostoClp { get; set; }
    public decimal SharePct { get; set; }
    public bool Configurada => Origen != OrigenFactorPresentacion.SinConfigurar;
    public List<FamiliaPresentacionInfo> Familias { get; } = new();
}

/// <summary>
/// Informe de las familias de precio del proyecto: qué factor aplica a cada familia, de dónde
/// sale y cuánto costo representa. Los factores salen del motor (BudgetSettings) para que la
/// pantalla nunca muestre un número distinto al que se presupuesta.
/// </summary>
public sealed class InformePresentacionFamilias
{
    private readonly BudgetSettings _settings;
    private readonly Dictionary<string, decimal> _variables;
    private readonly Dictionary<string, string> _asignadas;

    internal InformePresentacionFamilias(
        BudgetSettings settings,
        Dictionary<string, decimal> variables,
        Dictionary<string, string> asignadas)
    {
        _settings = settings;
        _variables = variables;
        _asignadas = asignadas;
    }

    /// <summary>Familias con costo propio en la cubicación, ordenadas por impacto.</summary>
    public IReadOnlyList<FamiliaPresentacionInfo> Familias { get; internal set; } = Array.Empty<FamiliaPresentacionInfo>();

    /// <summary>Filas canónicas con su factor, origen e impacto.</summary>
    public IReadOnlyList<BucketPresentacionInfo> Buckets { get; internal set; } = Array.Empty<BucketPresentacionInfo>();

    /// <summary>True cuando el proyecto vende por factores de familia.</summary>
    public bool UsaFactoresPorFamilia { get; internal set; }

    /// <summary>Costo total de la cubicación cubierto por las familias.</summary>
    public decimal CostoTotalClp { get; internal set; }

    /// <summary>Cantidad de familias usadas que no tienen factor configurado.</summary>
    public int FamiliasSinConfigurar { get; internal set; }

    /// <summary>Resuelve el factor y el origen de cualquier familia, usada o no.</summary>
    public FamiliaPresentacionInfo Resolver(string? nombre)
        => PresentacionFamiliasInforme.ResolverFamilia(nombre ?? "", _variables, _asignadas, _settings);
}

/// <summary>Reglas de presentación por familia: resolución de factor, origen e impacto.</summary>
public static class PresentacionFamiliasInforme
{
    /// <summary>Filas canónicas siempre disponibles.</summary>
    public static readonly IReadOnlyList<string> FamiliasCanonicas = new[]
    {
        "Conductores",
        "Canalización",
        "Tableros",
        "Protecciones",
        "Iluminación",
        "Equipos",
        "Enchufes y tomacorrientes",
        "Puesta a tierra",
        "Accesorios",
        "Movimientos",
        "Comisionamiento"
    };

    private static readonly Dictionary<string, string> Etiquetas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Conductores"] = "MATERIALES",
        ["Canalización"] = "PVC/EPC",
        ["Tableros"] = "TABLEROS",
        ["Protecciones"] = "PROTECCIONES",
        ["Iluminación"] = "LUMINARIAS",
        ["Equipos"] = "EQUIPOS",
        ["Enchufes y tomacorrientes"] = "ARTEFACTOS",
        ["Puesta a tierra"] = "PUESTA A TIERRA",
        ["Accesorios"] = "VARIOS",
        ["Movimientos"] = "MOVIMIENTOS",
        ["Comisionamiento"] = "COMISIONAMIENTO"
    };

    /// <summary>Construye el informe a partir del proyecto y su cubicación.</summary>
    public static InformePresentacionFamilias Construir(ProjectInfoRow? project, IEnumerable<CubicacionRow>? cubicacion)
    {
        project ??= new ProjectInfoRow();
        var settings = BudgetSettings.FromProject(project);
        var variables = project.Variables ?? new Dictionary<string, decimal>();
        var asignadas = project.FamiliasPrecioAsignadas ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var informe = new InformePresentacionFamilias(settings, variables, asignadas)
        {
            UsaFactoresPorFamilia = settings.Markup?.FactoresPresentacionPorFamilia ?? false
        };
        if (!informe.UsaFactoresPorFamilia)
            return informe;

        var costoPorFamilia = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in cubicacion ?? Enumerable.Empty<CubicacionRow>())
        {
            if (row == null)
                continue;
            var nombre = (row.Categoria ?? "").Trim();
            var costo = row.Cantidad * (row.MatClpConFallback + row.MoClpConFallback);
            costoPorFamilia[nombre] = costoPorFamilia.TryGetValue(nombre, out var actual) ? actual + costo : costo;
        }

        var total = costoPorFamilia.Values.Sum();
        informe.CostoTotalClp = total;
        decimal porDefecto = project.MarkupFactorPorDefecto;

        var familias = new List<FamiliaPresentacionInfo>();
        foreach (var par in costoPorFamilia.OrderByDescending(p => p.Value))
        {
            var info = ResolverFamilia(par.Key, variables, asignadas, settings);
            info.CostoClp = par.Value;
            info.SharePct = total > 0m ? par.Value / total * 100m : 0m;
            info.UsadaEnCubicacion = true;
            familias.Add(info);
        }
        informe.Familias = familias;

        var buckets = new Dictionary<string, BucketPresentacionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonica in FamiliasCanonicas)
        {
            var clave = FamilyPresentationDefaults.Normalize(canonica);
            buckets[clave] = new BucketPresentacionInfo
            {
                Nombre = canonica,
                Clave = clave,
                Etiqueta = Etiquetas.TryGetValue(canonica, out var et) ? et : canonica.ToUpperInvariant()
            };
        }

        foreach (var info in familias)
        {
            if (!buckets.TryGetValue(info.ClaveObjetivo, out var bucket))
            {
                bucket = new BucketPresentacionInfo
                {
                    Nombre = info.NombreObjetivo,
                    Clave = info.ClaveObjetivo,
                    Etiqueta = info.NombreObjetivo.ToUpperInvariant()
                };
                buckets[info.ClaveObjetivo] = bucket;
            }
            bucket.Familias.Add(info);
            bucket.CostoClp += info.CostoClp;
        }

        foreach (var bucket in buckets.Values)
        {
            var markupPropio = variables.TryGetValue($"MarkupFactor_{bucket.Clave}", out var propio) ? propio : (decimal?)null;
            bucket.Markup = markupPropio ?? FamilyPresentationDefaults.DefaultMarkup(bucket.Clave, porDefecto);
            bucket.DescuentoPct = variables.TryGetValue($"MarkupDiscount_{bucket.Clave}", out var dto)
                ? MarkupPolicy.ClampPct(dto)
                : 0m;
            bucket.FactorEfectivo = ExcelCostosBridge.PresentationFactor(bucket.Markup, bucket.DescuentoPct);
            bucket.Origen = markupPropio.HasValue ? OrigenFactorPresentacion.Propio : OrigenFactorPresentacion.SinConfigurar;
            bucket.SharePct = total > 0m ? bucket.CostoClp / total * 100m : 0m;

            var tieneAsignadas = bucket.Familias.Any(f => f.Origen == OrigenFactorPresentacion.Asignada);
            bucket.FactorAplicado = tieneAsignadas
                ? FamilyPresentationDefaults.ResolveFactor(variables, bucket.Clave)
                : (settings.TryResolvePresentationFactor(bucket.Nombre, out var f) ? f : settings.DefaultPresentationFactor);
        }

        informe.Buckets = buckets.Values
            .OrderByDescending(b => b.CostoClp)
            .ThenBy(b => b.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        informe.FamiliasSinConfigurar = familias.Count(f => !f.Configurada);
        return informe;
    }

    /// <summary>Resuelve factor, origen y familia objetivo de una familia concreta.</summary>
    internal static FamiliaPresentacionInfo ResolverFamilia(
        string nombre,
        IReadOnlyDictionary<string, decimal> variables,
        IReadOnlyDictionary<string, string> asignadas,
        BudgetSettings settings)
    {
        var norm = FamilyPresentationDefaults.Normalize(nombre);
        var esCanonica = FamilyPresentationDefaults.IsCanonical(norm);
        var objetivo = norm;
        var origen = OrigenFactorPresentacion.Propio;

        if (!esCanonica)
        {
            if (asignadas.TryGetValue(norm, out var asignada))
            {
                var claveAsignada = FamilyPresentationDefaults.Normalize(asignada);
                if (FamilyPresentationDefaults.IsCanonical(claveAsignada))
                {
                    objetivo = claveAsignada;
                    origen = OrigenFactorPresentacion.Asignada;
                }
            }

            if (origen != OrigenFactorPresentacion.Asignada)
            {
                var natural = FamilyPresentationDefaults.NaturalCanonicalTarget(norm);
                objetivo = FamilyPresentationDefaults.IsCanonical(natural) ? natural : norm;
                origen = OrigenFactorPresentacion.Natural;
            }
        }

        var tienePropio = variables.ContainsKey($"MarkupFactor_{norm}");
        var markupObjetivo = variables.TryGetValue($"MarkupFactor_{objetivo}", out var mo) ? mo : (decimal?)null;

        if (esCanonica)
        {
            origen = markupObjetivo.HasValue ? OrigenFactorPresentacion.Propio : OrigenFactorPresentacion.SinConfigurar;
        }
        else if (!markupObjetivo.HasValue)
        {
            origen = OrigenFactorPresentacion.SinConfigurar;
        }

        var factor = settings.TryResolvePresentationFactor(nombre, out var efectivo)
            ? efectivo
            : settings.DefaultPresentationFactor;

        return new FamiliaPresentacionInfo
        {
            Nombre = string.IsNullOrWhiteSpace(nombre) ? "(sin familia)" : nombre,
            Clave = norm,
            NombreObjetivo = NombreDeObjetivo(objetivo),
            ClaveObjetivo = objetivo,
            Origen = origen,
            FactorEfectivo = factor,
            TieneValorPropioIgnorado = !esCanonica && tienePropio && origen == OrigenFactorPresentacion.Asignada
        };
    }

    private static string NombreDeObjetivo(string clave)
    {
        foreach (var canonica in FamiliasCanonicas)
        {
            if (string.Equals(FamilyPresentationDefaults.Normalize(canonica), clave, StringComparison.OrdinalIgnoreCase))
                return canonica;
        }
        return clave;
    }
}
