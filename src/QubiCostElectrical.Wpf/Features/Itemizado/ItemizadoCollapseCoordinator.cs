using System.Collections.Generic;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public static class ItemizadoCollapseCoordinator
{
    public static void ToggleChapter(ItemizadoRowVm? chapterRow, IReadOnlyList<ItemizadoRowVm> allRows)
    {
        if (chapterRow == null || !chapterRow.EsTitulo)
            return;

        chapterRow.IsCollapsed = !chapterRow.IsCollapsed;
        UpdateHierarchy(allRows);
    }

    public static void CollapseAll(IReadOnlyList<ItemizadoRowVm> allRows)
    {
        for (int i = 0; i < allRows.Count; i++)
        {
            if (allRows[i].EsTitulo)
                allRows[i].IsCollapsed = true;
        }
        UpdateHierarchy(allRows);
    }

    public static void ExpandAll(IReadOnlyList<ItemizadoRowVm> allRows)
    {
        for (int i = 0; i < allRows.Count; i++)
        {
            if (allRows[i].EsTitulo)
                allRows[i].IsCollapsed = false;
        }
        UpdateHierarchy(allRows);
    }

    public static void UpdateHierarchy(IReadOnlyList<ItemizadoRowVm> allRows)
    {
        var collapsedStack = new Stack<ItemizadoRowVm>();
        for (int i = 0; i < allRows.Count; i++)
        {
            var row = allRows[i];
            if (row.EsTitulo)
            {
                while (collapsedStack.Count > 0 && collapsedStack.Peek().WbsLevel >= row.WbsLevel)
                {
                    collapsedStack.Pop();
                }

                row.IsHiddenByParentCollapse = collapsedStack.Count > 0;

                if (row.IsCollapsed)
                {
                    collapsedStack.Push(row);
                }

                int directPartidas = 0;
                int completedPartidas = 0;
                for (int j = i + 1; j < allRows.Count; j++)
                {
                    var next = allRows[j];
                    if (next.EsTitulo && next.WbsLevel <= row.WbsLevel)
                        break;
                    if (!next.EsTitulo)
                    {
                        directPartidas++;
                        if (next.CompletionState == PartidaCompletionState.Completed || next.StatusKind == "ok")
                            completedPartidas++;
                    }
                }
                row.SetChapterStats(directPartidas, completedPartidas);
            }
            else
            {
                row.IsHiddenByParentCollapse = collapsedStack.Count > 0;
            }
        }
    }
}
