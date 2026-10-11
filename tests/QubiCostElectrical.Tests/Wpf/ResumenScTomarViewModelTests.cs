using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ResumenScTomarViewModelTests
{
    [Fact]
    public void ResumenScTomarViewModel_CountsAndCategoryFilters_Work()
    {
        var zonas = new List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Piso 1", Activa = true, Orden = 1 },
            new() { Id = "z2", Nombre = "Piso 2", Activa = true, Orden = 2 }
        };

        var config = CubicacionScConfig.Default();
        var cuadros = new List<CuadroCargaSCRow>
        {
            new()
            {
                Nombre = "TDA-SC",
                Circuitos = new List<CircuitoSCRow>
                {
                    new()
                    {
                        Numero = 1,
                        Descripcion = "Alumbrado Pasillo",
                        MetrosTuberia = 30,
                        TipoTuberia = "PVCLH",
                        SeccionTuberia = "20mm",
                        ConductorDistribucion = "H07V-U",
                        SeccionDistribucion = "2.5mm2",
                        Ubicacion = "Piso 1"
                    }
                }
            }
        };

        var vm = new ResumenScTomarViewModel(cuadros, config, zonas);

        Assert.Equal(2, vm.ZonasCount);
        Assert.True(vm.TotalMaterialesCount >= 1);
        Assert.True(vm.CategoriasCount >= 1);

        // FlatRowVm properties
        var header = new GroupHeaderRowVm { Nombre = "Tubería", TotalMl = 30, MaterialCount = 1 };
        Assert.True(header.IsGroupHeader);
        Assert.False(header.IsMaterial);
        Assert.Equal("📁 TUBERÍA (1 materiales)", header.CodigoDisplay);

        var mat = new MaterialConZonasVm { Nombre = "Tubo PVC 20mm", Total = 30 };
        Assert.False(mat.IsGroupHeader);
        Assert.True(mat.IsMaterial);
    }
}
