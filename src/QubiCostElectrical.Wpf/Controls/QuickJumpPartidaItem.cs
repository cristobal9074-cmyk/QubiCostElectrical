using System;

namespace QubiCostElectrical.Wpf.Controls;

public sealed class QuickJumpPartidaItem
{
    public string Id { get; init; } = "";
    public string Codigo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Unidad { get; init; } = "";
    public decimal Cantidad { get; init; }
    public bool EsTitulo { get; init; }
    public string? BadgeText { get; init; }
    public string? BadgeColor { get; init; }
    public string? Capitulo { get; init; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Codigo)
        ? Descripcion
        : $"{Codigo}  {Descripcion}";

    public string CantidadUnidadText => !EsTitulo && Cantidad > 0m
        ? $"{Cantidad:N2} {Unidad}".Trim()
        : (!EsTitulo && !string.IsNullOrWhiteSpace(Unidad) ? Unidad : "");

    public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);
    public bool HasCapitulo => !string.IsNullOrWhiteSpace(Capitulo);
}
