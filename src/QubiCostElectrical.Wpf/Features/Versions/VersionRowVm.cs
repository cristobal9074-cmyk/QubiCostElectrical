using System;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed class VersionRowVm
{
    private readonly VersionsCurrencyFormat? currency;

    public VersionRowVm(BudgetVersionRow source, bool isActive, decimal deltaUf, VersionsCurrencyFormat? currency = null)
    {
        Source = source;
        IsActive = isActive;
        DeltaUf = deltaUf;
        this.currency = currency;
    }

    public BudgetVersionRow Source { get; }
    public bool IsActive { get; }
    public decimal DeltaUf { get; }
    public string Codigo => Source.Codigo;
    public string Nombre => Source.Nombre;
    public string Estado => Source.Estado;
    public string CreadoPor => Source.CreadoPor;
    public DateTime FechaCreacion => Source.FechaCreacion;
    public DateTime FechaModificacion => Source.FechaModificacion;
    public decimal MontoVentaUf => Source.MontoVentaUf;

    // MontoVentaUf/DeltaUf son pesos chilenos (nombre legado). Con el valor de moneda del
    // proyecto se muestra "1.234 UF" o "$ 48.000"; sin contexto (tests) se mantiene el N3.
    public string MontoVentaText => currency is null
        ? $"{MontoVentaUf:N3}"
        : currency.FromClp(MontoVentaUf);

    public string DeltaText => DeltaUf == 0m
        ? "—"
        : currency is null
            ? $"{(DeltaUf > 0m ? "+" : string.Empty)}{DeltaUf:N3}"
            : currency.FromClpSigned(DeltaUf);

    public string DeltaKind => DeltaUf > 0m ? "Aumenta" : DeltaUf < 0m ? "Reduce" : "Neutro";
    public string RevBadge => string.IsNullOrWhiteSpace(Codigo) ? "—" : Codigo.Length <= 3 ? Codigo : Codigo[^3..];

    // Iniciales del autor (avatar de la tabla de historial): "C. Rojas" → "CR".
    public string Iniciales
    {
        get
        {
            var partes = (CreadoPor ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (partes.Length == 0) return "—";
            if (partes.Length == 1) return partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant();
            return $"{partes[0][0]}{partes[^1][0]}".ToUpperInvariant();
        }
    }
}

public enum RevisionDiffFilterMode
{
    Todos = 0,
    SoloAdicionales = 1,
    SoloModificados = 2,
    SoloDeductivos = 3,
    SoloSinCambios = 4
}
