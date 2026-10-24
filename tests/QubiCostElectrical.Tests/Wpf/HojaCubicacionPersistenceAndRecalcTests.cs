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

        // Validar que ToSaved preserva IsPicked y PickedTargetKey
        vm.Filas[1].PickedTargetKey = "row:abc:qty";
        var roundtrip = vm.ToSaved();
        Assert.True(roundtrip.IsPicked);
        Assert.True(roundtrip.Filas[1].IsPicked);
        Assert.Equal("row:abc:qty", roundtrip.Filas[1].PickedTargetKey);
        Assert.False(roundtrip.Filas[2].IsPicked);

        var restored = HojaCubicacionItemVm.FromSaved(roundtrip);
        Assert.Equal("row:abc:qty", restored.Filas[1].PickedTargetKey);
    }

    [Fact]
    public void ReemplazarSeleccion_DesmarcaFilaAnteriorConMismoTargetKey()
    {
        var item = new HojaCubicacionItemVm("1.1", "Tuberías", "ml", 4);
        var f1 = item.AddFila(4, "Fila 1");
        var f2 = item.AddFila(4, "Fila 2");
        f1.Valores[0] = "10";
        f2.Valores[0] = "20";
        item.RefreshAllDisplays();

        // Simular primera selección para row:partida1
        string targetKey = "row:partida1";
        f1.IsPicked = true;
        f1.PickedTargetKey = targetKey;

        Assert.True(f1.IsPicked);
        Assert.Equal(targetKey, f1.PickedTargetKey);
        Assert.False(f2.IsPicked);

        // Si el usuario se equivocó y reasigna para el mismo targetKey a la Fila 2:
        // Se desmarca la anterior con el mismo targetKey y se marca la nueva
        foreach (var f in item.Filas)
        {
            if (f.PickedTargetKey == targetKey)
            {
                f.IsPicked = false;
                f.PickedTargetKey = null;
            }
        }
        f2.IsPicked = true;
        f2.PickedTargetKey = targetKey;

        Assert.False(f1.IsPicked);
        Assert.Null(f1.PickedTargetKey);
        Assert.True(f2.IsPicked);
        Assert.Equal(targetKey, f2.PickedTargetKey);
    }
}
