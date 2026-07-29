using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Reordenamiento jerárquico del itemizado: capítulos mueven bloques completos;
/// partidas solo intercambian posición entre hermanas del mismo capítulo.
/// </summary>
public static class ItemizadoHierarchyMove
{
    public static ItemizadoMutationResult Move(List<ItemizadoRow> items, int index, int direction)
    {
        if (items == null || items.Count == 0 || index < 0 || index >= items.Count)
            return new ItemizadoMutationResult(index, Array.Empty<string>());

        if (direction == 0)
            return new ItemizadoMutationResult(index, Array.Empty<string>());

        return items[index].EsTitulo
            ? MoveTitleBlock(items, index, direction)
            : MovePartidaWithinChapter(items, index, direction);
    }

    public static int GetTitleBlockLength(IReadOnlyList<ItemizadoRow> items, int titleIndex)
        => GetTitleBlockEnd(items, titleIndex) - titleIndex + 1;

    public static int GetTitleBlockEnd(IReadOnlyList<ItemizadoRow> items, int titleIndex)
    {
        if (titleIndex < 0 || titleIndex >= items.Count || !items[titleIndex].EsTitulo)
            return titleIndex;

        int end = titleIndex;
        for (int i = titleIndex + 1; i < items.Count; i++)
        {
            if (items[i].EsTitulo)
                break;
            end = i;
        }

        return end;
    }

    private static ItemizadoMutationResult MoveTitleBlock(List<ItemizadoRow> items, int titleIndex, int direction)
    {
        int blockEnd = GetTitleBlockEnd(items, titleIndex);
        int blockLength = blockEnd - titleIndex + 1;

        if (direction < 0)
        {
            if (titleIndex == 0)
                return new ItemizadoMutationResult(titleIndex, Array.Empty<string>());

            int prevTitle = FindPreviousTitleIndex(items, titleIndex - 1);
            int insertAt = prevTitle >= 0 ? prevTitle : 0;
            var block = items.GetRange(titleIndex, blockLength);
            items.RemoveRange(titleIndex, blockLength);
            items.InsertRange(insertAt, block);
            return new ItemizadoMutationResult(insertAt, Array.Empty<string>());
        }

        if (blockEnd >= items.Count - 1)
            return new ItemizadoMutationResult(titleIndex, Array.Empty<string>());

        int nextTitle = FindNextTitleIndex(items, blockEnd + 1);
        if (nextTitle < 0)
            return new ItemizadoMutationResult(titleIndex, Array.Empty<string>());

        int nextBlockEnd = GetTitleBlockEnd(items, nextTitle);
        int nextBlockLength = nextBlockEnd - nextTitle + 1;

        var moving = items.GetRange(titleIndex, blockLength);
        var other = items.GetRange(nextTitle, nextBlockLength);
        items.RemoveRange(nextTitle, nextBlockLength);
        items.RemoveRange(titleIndex, blockLength);
        items.InsertRange(titleIndex, other);
        items.InsertRange(titleIndex + other.Count, moving);
        return new ItemizadoMutationResult(titleIndex + other.Count, Array.Empty<string>());
    }

    private static ItemizadoMutationResult MovePartidaWithinChapter(List<ItemizadoRow> items, int index, int direction)
    {
        int target = index + Math.Sign(direction);
        if (target < 0 || target >= items.Count)
            return new ItemizadoMutationResult(index, Array.Empty<string>());

        var row = items[index];
        var other = items[target];
        if (other.EsTitulo)
            return new ItemizadoMutationResult(index, Array.Empty<string>());

        string parent = ItemizadoHierarchyRules.ParentIdOf(row.Id, items);
        string otherParent = ItemizadoHierarchyRules.ParentIdOf(other.Id, items);
        if (!string.Equals(parent, otherParent, StringComparison.OrdinalIgnoreCase))
            return new ItemizadoMutationResult(index, Array.Empty<string>());

        (items[index], items[target]) = (items[target], items[index]);
        return new ItemizadoMutationResult(target, Array.Empty<string>());
    }

    private static int FindPreviousTitleIndex(IReadOnlyList<ItemizadoRow> items, int fromIndex)
    {
        for (int i = fromIndex; i >= 0; i--)
        {
            if (items[i].EsTitulo)
                return i;
        }

        return -1;
    }

    private static int FindNextTitleIndex(IReadOnlyList<ItemizadoRow> items, int fromIndex)
    {
        for (int i = fromIndex; i < items.Count; i++)
        {
            if (items[i].EsTitulo)
                return i;
        }

        return -1;
    }
}
