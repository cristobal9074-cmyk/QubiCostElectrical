namespace QubiCostElectrical;

public sealed record Currency
{
    public string IsoCode { get; init; } = "";
    public string Symbol { get; init; } = "";
    public string Name { get; init; } = "";
    public int DecimalPlaces { get; init; } = 2;
    public bool IsActive { get; init; } = true;
}

public sealed record ExchangeRate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceCurrency { get; init; } = "";
    public string TargetCurrency { get; init; } = "";
    public decimal Rate { get; init; }
    public DateTime Date { get; init; } = DateTime.UtcNow;
    public string Source { get; init; } = "Manual";
    public string UserId { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool IsActive { get; init; } = true;
}

public static class CurrencyCatalog
{
    public static readonly Currency CLP = new() { IsoCode = "CLP", Symbol = "$", Name = "Peso chileno", DecimalPlaces = 0 };
    public static readonly Currency USD = new() { IsoCode = "USD", Symbol = "US$", Name = "Dólar estadounidense", DecimalPlaces = 2 };
    public static readonly Currency EUR = new() { IsoCode = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2 };
    public static readonly Currency UF = new() { IsoCode = "UF", Symbol = "UF", Name = "Unidad de fomento", DecimalPlaces = 2 };

    private static readonly Dictionary<string, Currency> ByCode = new()
    {
        [CLP.IsoCode] = CLP,
        [USD.IsoCode] = USD,
        [EUR.IsoCode] = EUR,
        [UF.IsoCode] = UF
    };

    public static IReadOnlyDictionary<string, Currency> All => ByCode;
    public static Currency? Find(string? code) => code != null && ByCode.TryGetValue(code.Trim().ToUpperInvariant(), out var c) ? c : null;
    public static bool IsValid(string? code) => Find(code) != null;
    public static Currency GetDefault() => CLP;

    public static decimal Convert(decimal amount, ExchangeRate rate)
    {
        if (rate.Rate <= 0) throw new ArgumentException("Exchange rate must be positive", nameof(rate));
        return PrecisionPolicy.RoundImporte(amount * rate.Rate);
    }
}
