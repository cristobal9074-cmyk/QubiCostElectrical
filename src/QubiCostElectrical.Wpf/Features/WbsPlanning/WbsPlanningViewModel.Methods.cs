using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.WbsPlanning;

public sealed partial class WbsPlanningViewModel
{
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            StatusMessage = "Cargando estructura WBS...";
            var repo = host.Services.WbsPlanningRepository;
            var projId = host.Session.CurrentProjectPath;
            var scheduleRepo = host.Services.ScheduleRepository;
            var schedules = await scheduleRepo.GetByProjectAsync(projId);
            currentScheduleId = schedules.FirstOrDefault()?.Id;
            if (currentScheduleId == null)
            {
                StatusMessage = "No hay cronograma activo. Cree uno primero.";
                return;
            }
            var tree = await repo.GetByScheduleAsync(currentScheduleId);
            nodes.Clear();
            foreach (var n in tree.OrderBy(x => x.SortOrder).ThenBy(x => x.WbsCode))
            {
                nodes.Add(new WbsNodeVm
                {
                    Id = n.Id,
                    WbsCode = n.WbsCode,
                    Name = n.Name,
                    NodeType = n.NodeType,
                    Level = n.Level,
                    Status = n.Status,
                    Responsible = n.Responsible,
                    SortOrder = n.SortOrder
                });
            }
            StatusMessage = $"{nodes.Count} nodo(s) cargado(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar WBS: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddNodeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNodeCode) || string.IsNullOrWhiteSpace(NewNodeName)) return;
        if (currentScheduleId == null) return;
        try
        {
            StatusMessage = "Agregando nodo...";
            var repo = host.Services.WbsPlanningRepository;
            var id = WbsPlanningNodeId.New();
            var parentId = SelectedNode?.Id;
            var sortOrder = parentId != null
                ? await repo.GetMaxSortOrderAsync(currentScheduleId, parentId)
                : await repo.GetMaxSortOrderAsync(currentScheduleId, null);
            var node = new WbsPlanningNode(id, currentScheduleId, NewNodeCode.Trim(), NewNodeName.Trim(), NewNodeType, host.Session.CurrentUserName)
            {
                ParentId = parentId,
                SortOrder = sortOrder + 1,
                Level = SelectedNode?.Level != null ? SelectedNode.Level + 1 : 1
            };
            await repo.SaveAsync(node);
            NewNodeCode = "";
            NewNodeName = "";
            await LoadAsync();
            StatusMessage = "Nodo WBS agregado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al agregar nodo: {ex.Message}";
        }
    }

    public async Task DeleteNodeAsync()
    {
        var node = SelectedNode;
        if (node == null) return;
        try
        {
            StatusMessage = "Eliminando nodo...";
            var repo = host.Services.WbsPlanningRepository;
            await repo.DeleteAsync(node.Id);
            SelectedNode = null;
            await LoadAsync();
            StatusMessage = "Nodo WBS eliminado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al eliminar nodo: {ex.Message}";
        }
    }

    private void Reset()
    {
        nodes.Clear();
        NewNodeCode = "";
        NewNodeName = "";
        currentScheduleId = null;
        StatusMessage = "";
    }
}
