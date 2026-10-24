using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class HojaCubicacionPersistenceAndRecalcTests
{
    [Fact]
    public void FromSaved_RecalculaValoresDisplayYSumaInmediatamente()
    {
        var savedItem = new HojaCubicacionItem
        {
            Codigo = "1.1",
            Titulo = "Tubería PVC",
            Unidad = "ml",
            IsPicked = true,
            Filas = new List<HojaCubicacionFila>
            {
                new() { Descripcion = "Encabezados", IsLabelRow = true, Valores = new List<string> { "S1", "S2", "S3", "S4" } },
                new() { Descripcion = "pvc20", IsLabelRow = false, IsPicked = true, Valores = new List<string> { "100", "100", "100", "100" } },
                new() { Descripcion = "pvc1", IsLabelRow = false, IsPicked = false, Valores = new List<string> { "250", "250" } }
            }
        };

        var vm = HojaCubicacionItemVm.FromSaved(savedItem);

        Assert.True(vm.IsPicked);
        Assert.Equal(3, vm.Filas.Count);

        var fila1 = vm.Filas[1];
        Assert.True(fila1.IsPicked);
        Assert.Equal("100", fila1.ValoresDisplay[0]);
        Assert.Equal("100", fila1.ValoresDisplay[1]);
        Assert.Equal("100", fila1.ValoresDisplay[2]);
        Assert.Equal("100", fila1.ValoresDisplay[3]);
        Assert.Equal(400m, fila1.Suma);
        Assert.Equal("400", fila1.SumaDisplay);

        var fila2 = vm.Filas[2];
        Assert.False(fila2.IsPicked);
        Assert.Equal("250", fila2.ValoresDisplay[0]);
        Assert.Equal("250", fila2.ValoresDisplay[1]);
        Assert.Equal(500m, fila2.Suma);
        Assert.Equal("500", fila2.SumaDisplay);

        Assert.Equal(900m, vm.Subtotal);

        // Validar que ToSaved preserva IsPicked
        var roundtrip = vm.ToSaved();
        Assert.True(roundtrip.IsPicked);
        Assert.True(roundtrip.Filas[1].IsPicked);
        Assert.False(roundtrip.Filas[2].IsPicked);
    }
}
