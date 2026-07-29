using System.Collections.ObjectModel;

namespace QubiCostElectrical;

public sealed record UnitOfMeasure
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Symbol { get; init; } = "";
    public UnitCategory Category { get; init; } = UnitCategory.Quantity;
    public bool IsActive { get; init; } = true;
}

public enum UnitCategory
{
    Quantity,
    Length,
    Area,
    Volume,
    Mass,
    Time,
    Percentage,
    Global
}

public static class UnitOfMeasureCatalog
{
    public static readonly UnitOfMeasure Unidad = new() { Code = "UN", Name = "Unidad", Symbol = "un", Category = UnitCategory.Quantity };
    public static readonly UnitOfMeasure Metro = new() { Code = "ML", Name = "Metro lineal", Symbol = "ml", Category = UnitCategory.Length };
    public static readonly UnitOfMeasure MetroCuadrado = new() { Code = "M2", Name = "Metro cuadrado", Symbol = "m²", Category = UnitCategory.Area };
    public static readonly UnitOfMeasure MetroCubico = new() { Code = "M3", Name = "Metro cúbico", Symbol = "m³", Category = UnitCategory.Volume };
    public static readonly UnitOfMeasure Kilogramo = new() { Code = "KG", Name = "Kilogramo", Symbol = "kg", Category = UnitCategory.Mass };
    public static readonly UnitOfMeasure Tonelada = new() { Code = "TON", Name = "Tonelada", Symbol = "ton", Category = UnitCategory.Mass };
    public static readonly UnitOfMeasure Litro = new() { Code = "LTR", Name = "Litro", Symbol = "L", Category = UnitCategory.Volume };
    public static readonly UnitOfMeasure Hora = new() { Code = "HH", Name = "Hora", Symbol = "h", Category = UnitCategory.Time };
    public static readonly UnitOfMeasure Jornada = new() { Code = "JOR", Name = "Jornada", Symbol = "jor", Category = UnitCategory.Time };
    public static readonly UnitOfMeasure Lote = new() { Code = "LTE", Name = "Lote", Symbol = "lote", Category = UnitCategory.Quantity };
    public static readonly UnitOfMeasure Porcentaje = new() { Code = "PCT", Name = "Porcentaje", Symbol = "%", Category = UnitCategory.Percentage };
    public static readonly UnitOfMeasure Global = new() { Code = "GL", Name = "Global", Symbol = "gl", Category = UnitCategory.Global };

    private static readonly Dictionary<string, UnitOfMeasure> ByCode = new()
    {
        [Unidad.Code] = Unidad,
        [Metro.Code] = Metro,
        [MetroCuadrado.Code] = MetroCuadrado,
        [MetroCubico.Code] = MetroCubico,
        [Kilogramo.Code] = Kilogramo,
        [Tonelada.Code] = Tonelada,
        [Litro.Code] = Litro,
        [Hora.Code] = Hora,
        [Jornada.Code] = Jornada,
        [Lote.Code] = Lote,
        [Porcentaje.Code] = Porcentaje,
        [Global.Code] = Global
    };

    public static IReadOnlyDictionary<string, UnitOfMeasure> All => new ReadOnlyDictionary<string, UnitOfMeasure>(ByCode);
    public static UnitOfMeasure? Find(string? code) => code != null && ByCode.TryGetValue(code.Trim().ToUpperInvariant(), out var u) ? u : null;
    public static bool IsValid(string? code) => Find(code) != null;
    public static UnitOfMeasure GetDefault() => Unidad;
}
