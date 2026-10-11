using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

/// <summary>
/// Nodo individual en la ruta de jerarquía persistente (Breadcrumb) del Itemizado.
/// </summary>
public sealed record ItemizadoBreadcrumbNode(
    string Item,
    string Descripcion,
    bool IsCurrent,
    ItemizadoRowVm RowVm)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Item)
        ? Descripcion
        : $"{Item} {Descripcion}";
}

/// <summary>
/// Coordina la reconstrucción continua y navegación del breadcrumb contextual
/// de capítulos y subcapítulos según la fila activa del Itemizado.
/// </summary>
public sealed class ItemizadoBreadcrumbCoordinator
{
    private readonly Action<ItemizadoRowVm> onSelectRow;

    public ObservableCollection<ItemizadoBreadcrumbNode> Nodes { get; } = new();

    public RelayCommand<ItemizadoBreadcrumbNode> NavigateToNodeCommand { get; }

    public bool HasNodes => Nodes.Count > 0;

    public ItemizadoBreadcrumbCoordinator(Action<ItemizadoRowVm> onSelectRow)
    {
        this.onSelectRow = onSelectRow ?? throw new ArgumentNullException(nameof(onSelectRow));
        NavigateToNodeCommand = new RelayCommand<ItemizadoBreadcrumbNode>(
            node =>
            {
                if (node?.RowVm != null)
                {
                    this.onSelectRow(node.RowVm);
                }
            },
            node => node?.RowVm != null && !node.IsCurrent);
    }

    public void UpdateBreadcrumb(
        ItemizadoRowVm? selectedRow,
        IReadOnlyDictionary<string, ItemizadoRowVm> rowsById,
        IReadOnlyList<ItemizadoRowVm> allRows)
    {
        Nodes.Clear();

        if (selectedRow == null)
            return;

        var chain = new List<ItemizadoBreadcrumbNode>();
        var current = selectedRow;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool isLeaf = true;
        while (current != null && visited.Add(current.Id))
        {
            chain.Insert(0, new ItemizadoBreadcrumbNode(
                current.Item,
                current.Descripcion,
                isLeaf,
                current));

            isLeaf = false;

            // 1. Intentar resolver por ParentId explícito
            if (!string.IsNullOrWhiteSpace(current.Model.ParentId) &&
                rowsById.TryGetValue(current.Model.ParentId, out var parentRow))
            {
                current = parentRow;
                continue;
            }

            // 2. Fallback: Buscar hacia atrás en la lista el título precedente con nivel menor
            current = FindPrecedingParent(current, allRows);
        }

        foreach (var node in chain)
        {
            Nodes.Add(node);
        }
    }

    private static ItemizadoRowVm? FindPrecedingParent(ItemizadoRowVm current, IReadOnlyList<ItemizadoRowVm> allRows)
    {
        int currentIndex = -1;
        for (int i = 0; i < allRows.Count; i++)
        {
            if (ReferenceEquals(allRows[i], current) || string.Equals(allRows[i].Id, current.Id, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex <= 0)
            return null;

        int targetLevel = current.WbsLevel;
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            var candidate = allRows[i];
            if (candidate.EsTitulo && candidate.WbsLevel < targetLevel)
            {
                return candidate;
            }
        }

        return null;
    }
}
