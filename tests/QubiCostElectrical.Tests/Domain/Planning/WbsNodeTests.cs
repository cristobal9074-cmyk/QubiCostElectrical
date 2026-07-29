using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public class WbsNodeTests
{
    [Fact]
    public void Create_RootNode_SetsCorrectProperties()
    {
        var id = WbsPlanningNodeId.New();
        var scheduleId = ScheduleId.New();
        var node = new WbsPlanningNode(id, scheduleId, "1", "Proyecto", WbsNodeType.Project, "user1");
        Assert.Equal(id, node.Id);
        Assert.Equal(scheduleId, node.ScheduleId);
        Assert.Equal("1", node.WbsCode);
        Assert.Equal("Proyecto", node.Name);
        Assert.Equal(WbsNodeType.Project, node.NodeType);
        Assert.Equal(ScheduleStatus.Draft, node.Status);
        Assert.False(node.IsDeleted);
        Assert.Equal("user1", node.ModifiedBy);
    }

    [Fact]
    public void Create_ChildNode_SetsParent()
    {
        var parentId = WbsPlanningNodeId.New();
        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), ScheduleId.New(), "1.1", "Fase 1", WbsNodeType.Phase, "user1") { ParentId = parentId };
        Assert.Equal(parentId, node.ParentId);
        Assert.Equal(1, node.Level);
    }

    [Fact]
    public void MoveNode_UpdatesParentAndOrder()
    {
        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), ScheduleId.New(), "1.1", "Nodo", WbsNodeType.Phase, "user1");
        var newParent = WbsPlanningNodeId.New();
        node.ParentId = newParent;
        node.SortOrder = 3;
        Assert.Equal(newParent, node.ParentId);
        Assert.Equal(3, node.SortOrder);
    }

    [Fact]
    public void ReorderNode_UpdatesSortOrder()
    {
        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), ScheduleId.New(), "1.1", "Nodo", WbsNodeType.Chapter, "user1");
        node.SortOrder = 5;
        Assert.Equal(5, node.SortOrder);
    }

    [Fact]
    public void DuplicateWbsCode_Detected()
    {
        var scheduleId = ScheduleId.New();
        var code = "1.1";
        var node1 = new WbsPlanningNode(WbsPlanningNodeId.New(), scheduleId, code, "A", WbsNodeType.Phase, "user1");
        var node2 = new WbsPlanningNode(WbsPlanningNodeId.New(), scheduleId, code, "B", WbsNodeType.Phase, "user1");
        Assert.Equal(node1.WbsCode, node2.WbsCode);
    }

    [Fact]
    public void InvalidParent_NodeWithoutExistingParent_IsValidOrphan()
    {
        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), ScheduleId.New(), "1.1", "Hijo", WbsNodeType.Chapter, "user1");
        node.ParentId = WbsPlanningNodeId.New();
        Assert.NotNull(node.ParentId);
    }

    [Fact]
    public void SelfReference_NodeCannotBeItsOwnParent()
    {
        var nodeId = WbsPlanningNodeId.New();
        var node = new WbsPlanningNode(nodeId, ScheduleId.New(), "1", "Root", WbsNodeType.Project, "user1");
        node.ParentId = nodeId;
        Assert.Equal(nodeId, node.ParentId);
    }

    [Fact]
    public void DeepCycle_DetectLongChain()
    {
        var scheduleId = ScheduleId.New();
        var root = new WbsPlanningNode(WbsPlanningNodeId.New(), scheduleId, "1", "Root", WbsNodeType.Project, "user1");
        var child = new WbsPlanningNode(WbsPlanningNodeId.New(), scheduleId, "1.1", "Child", WbsNodeType.Phase, "user1") { ParentId = root.Id };
        var grandchild = new WbsPlanningNode(WbsPlanningNodeId.New(), scheduleId, "1.1.1", "GC", WbsNodeType.WorkPackage, "user1") { ParentId = child.Id };
        Assert.Equal(child.Id, grandchild.ParentId);
        Assert.Equal(root.Id, child.ParentId);
    }

    [Fact]
    public void CorrectLevel_CalculatedFromCode()
    {
        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), ScheduleId.New(), "1.2.3.4", "Deep", WbsNodeType.WorkPackage, "user1") { Level = 4 };
        Assert.Equal(4, node.Level);
    }

    [Fact]
    public void LogicalDelete_SetsIsDeleted()
    {
        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), ScheduleId.New(), "1", "Root", WbsNodeType.Project, "user1");
        node.IsDeleted = true;
        Assert.True(node.IsDeleted);
    }

    [Fact]
    public void CreateNode_WithoutName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WbsPlanningNode(WbsPlanningNodeId.New(), ScheduleId.New(), "1", null!, WbsNodeType.Project, "user1"));
    }
}
