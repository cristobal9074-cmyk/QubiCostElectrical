using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class ExportProjectContextTests
{
    [Fact]
    public void IsDraft_DefaultsToTrue_WhenStatusMissing()
    {
        var ctx = new ExportProjectContext();

        Assert.True(ctx.IsDraft);
    }

    [Fact]
    public void IsDraft_ReturnsFalse_WhenProjectIsNotDraft()
    {
        var ctx = new ExportProjectContext
        {
            Data = new ProjectFileData
            {
                Proyecto = new ProjectInfoRow
                {
                    EstadoPresupuesto = "Aprobado"
                }
            }
        };

        Assert.False(ctx.IsDraft);
    }
}
