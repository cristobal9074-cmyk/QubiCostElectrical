using System;

namespace QubiCostElectrical.Wpf.Controls;

public enum QuickJumpItemKind
{
    Partida,
    Capitulo,
    TableroSc,
    CircuitoSc
}

public sealed class QuickJumpPartidaItem
{
    public string Id { get; init; } = "";
    public string Codigo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Unidad { get; init; } = "";
    public decimal Cantidad { get; init; }
    public bool EsTitulo { get; init; }
    public bool IsSelectable { get; init; } = true;
    public QuickJumpItemKind Kind { get; init; } = QuickJumpItemKind.Partida;
    public string? SecondaryId { get; init; }
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

    public string CategoryBadgeText => Kind switch
    {
        QuickJumpItemKind.Capitulo => "CAPÍTULO",
        QuickJumpItemKind.TableroSc => "TABLERO SC",
        QuickJumpItemKind.CircuitoSc => "CIRCUITO",
        _ => "PARTIDA"
    };

    public string CategoryBadgeBackground => Kind switch
    {
        QuickJumpItemKind.Capitulo => "#1A8B5CF6",
        QuickJumpItemKind.TableroSc => "#1AF59E0B",
        QuickJumpItemKind.CircuitoSc => "#1A10B981",
        _ => "#14007ACC"
    };

    public string CategoryBadgeForeground => Kind switch
    {
        QuickJumpItemKind.Capitulo => "#8B5CF6",
        QuickJumpItemKind.TableroSc => "#F59E0B",
        QuickJumpItemKind.CircuitoSc => "#10B981",
        _ => "#007ACC"
    };
}

