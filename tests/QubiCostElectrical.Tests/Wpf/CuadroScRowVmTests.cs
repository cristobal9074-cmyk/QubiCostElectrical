using System;
using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class CuadroScRowVmTests
{
    [Fact]
    public void PotenciaBadgeText_WithNoCircuits_ReturnsZero()
    {
        var row = new CuadroCargaSCRow { Nombre = "TDA SC", CircuitosProyectados = 20 };
        var vm = new CuadroScRowVm(row);

        Assert.Equal(0m, vm.TotalPotenciaW);
        Assert.Equal(0m, vm.TotalPotenciaKw);
        Assert.Equal(0m, vm.TotalPotenciaKva);
        Assert.Equal("0,0 kW / 0,0 kVA", vm.PotenciaBadgeText.Replace('.', ','));
    }

    [Fact]
    public void PotenciaBadgeText_WithCircuits_CalculatesKwAndKvaCorrectly()
    {
        var row = new CuadroCargaSCRow
        {
            Nombre = "TDA SC",
            CircuitosProyectados = 20,
            Circuitos = new List<CircuitoSCRow>
            {
                new() { Numero = 1, Tipo = "A", Estado = "Activo", PotenciaW = 2000m },
                new() { Numero = 2, Tipo = "E", Estado = "Activo", PotenciaW = 3000m },
                new() { Numero = 3, Tipo = "CL", Estado = "Disponible", PotenciaW = 4000m }, // Inactivo no suma
            }
        };

        var vm = new CuadroScRowVm(row);

        Assert.Equal(5000m, vm.TotalPotenciaW);
        Assert.Equal(5.0m, vm.TotalPotenciaKw);
        // 5.0 / 0.93 = 5.376 -> 5.4 kVA
        Assert.Equal(5.4m, vm.TotalPotenciaKva);
        Assert.Contains("5", vm.PotenciaBadgeText);
        Assert.Contains("kW", vm.PotenciaBadgeText);
        Assert.Contains("kVA", vm.PotenciaBadgeText);
    }

    [Fact]
    public void Capacidad_CalculatesPctAndEstadoCorrectly()
    {
        var row = new CuadroCargaSCRow
        {
            Nombre = "TDA 1",
            CircuitosProyectados = 10,
            Circuitos = new List<CircuitoSCRow>
            {
                new() { Numero = 1, Estado = "Activo" },
                new() { Numero = 2, Estado = "Activo" },
                new() { Numero = 3, Estado = "Activo" },
                new() { Numero = 4, Estado = "Activo" },
                new() { Numero = 5, Estado = "Activo" },
            }
        };

        var vm = new CuadroScRowVm(row);

        // 5 de 10 = 50% -> Normal
        Assert.Equal(50.0, vm.CapacidadPct);
        Assert.Equal(50.0, vm.CapacidadBarValue);
        Assert.Equal("Normal", vm.CapacidadEstado);
        Assert.Contains("5/10", vm.CapacidadText);

        // Subir a 8 activos (80%) -> Warning (> 75% alerta SEC)
        row.Circuitos.Add(new CircuitoSCRow { Numero = 6, Estado = "Activo" });
        row.Circuitos.Add(new CircuitoSCRow { Numero = 7, Estado = "Activo" });
        row.Circuitos.Add(new CircuitoSCRow { Numero = 8, Estado = "Activo" });
        vm.NotifyCircuitosActivosChanged();

        Assert.Equal(80.0, vm.CapacidadPct);
        Assert.Equal(80.0, vm.CapacidadBarValue);
        Assert.Equal("Warning", vm.CapacidadEstado);

        // Subir a 11 activos (110%) -> Overload (> 100%)
        row.Circuitos.Add(new CircuitoSCRow { Numero = 9, Estado = "Activo" });
        row.Circuitos.Add(new CircuitoSCRow { Numero = 10, Estado = "Activo" });
        row.Circuitos.Add(new CircuitoSCRow { Numero = 11, Estado = "Activo" });
        vm.NotifyCircuitosActivosChanged();

        Assert.Equal(110.0, vm.CapacidadPct);
        Assert.Equal(100.0, vm.CapacidadBarValue); // Clamp a 100
        Assert.Equal("Overload", vm.CapacidadEstado);
    }
}
