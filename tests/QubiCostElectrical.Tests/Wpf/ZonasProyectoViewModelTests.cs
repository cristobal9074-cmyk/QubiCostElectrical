using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ZonasProyectoViewModelTests
{
    [Fact]
    public void CargarZonas_PopulatesRowsAndComputesCockpitMetrics()
    {
        var vm = new ZonasProyectoViewModel();
        var zonas = new List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Subterráneo 2", Activa = true, Orden = 0 },
            new() { Id = "z2", Nombre = "Subterráneo 1", Activa = true, Orden = 1 },
            new() { Id = "z3", Nombre = "Piso 1", Activa = true, Orden = 2 },
            new() { Id = "z4", Nombre = "Azotea", Activa = false, Orden = 3 }
        };

        vm.CargarZonas(zonas);

        Assert.Equal(4, vm.TotalZonasCount);
        Assert.Equal(3, vm.ActivasCount);
        Assert.Equal(1, vm.InactivasCount);
        Assert.False(vm.HasNoZonas);

        Assert.Equal("01", vm.Rows[0].NumeroDisplay);
        Assert.Equal("Subterráneo", vm.Rows[0].TipoDisplay);
        Assert.Equal("Nivel", vm.Rows[2].TipoDisplay);
        Assert.Equal("Cubierta", vm.Rows[3].TipoDisplay);
    }

    [Fact]
    public void AgregarZona_AddsRowAndUpdatesSelection()
    {
        var vm = new ZonasProyectoViewModel();
        Assert.True(vm.HasNoZonas);

        var row = vm.AgregarZona("Piso 2");

        Assert.Single(vm.Rows);
        Assert.Equal(row, vm.SelectedRow);
        Assert.Equal(1, vm.TotalZonasCount);
        Assert.Equal(1, vm.ActivasCount);
        Assert.Equal(1, row.Numero);
        Assert.Equal("01", row.NumeroDisplay);
    }

    [Fact]
    public void MoveZona_UpAndDown_ReordersAndMaintainsSequence()
    {
        var vm = new ZonasProyectoViewModel();
        var rowA = vm.AgregarZona("Nivel A");
        var rowB = vm.AgregarZona("Nivel B");
        var rowC = vm.AgregarZona("Nivel C");

        Assert.Equal("Nivel A", vm.Rows[0].Nombre);
        Assert.Equal("Nivel B", vm.Rows[1].Nombre);
        Assert.Equal("Nivel C", vm.Rows[2].Nombre);

        // Mover Nivel C hacia arriba
        vm.MoveZona(rowC, -1);
        Assert.Equal("Nivel C", vm.Rows[1].Nombre);
        Assert.Equal("Nivel B", vm.Rows[2].Nombre);
        Assert.Equal(2, rowC.Numero);

        // Mover Nivel A hacia abajo
        vm.MoveZona(rowA, 1);
        Assert.Equal("Nivel C", vm.Rows[0].Nombre);
        Assert.Equal("Nivel A", vm.Rows[1].Nombre);
        Assert.Equal(1, rowC.Numero);
        Assert.Equal(2, rowA.Numero);
    }

    [Fact]
    public void SetActivasTodas_TogglesAllStates()
    {
        var vm = new ZonasProyectoViewModel();
        vm.AgregarZona("Zona 1");
        vm.AgregarZona("Zona 2");
        vm.AgregarZona("Zona 3");

        Assert.Equal(3, vm.ActivasCount);

        vm.SetActivasTodas(false);
        Assert.Equal(0, vm.ActivasCount);
        Assert.Equal(3, vm.InactivasCount);

        vm.SetActivasTodas(true);
        Assert.Equal(3, vm.ActivasCount);
        Assert.Equal(0, vm.InactivasCount);
    }

    [Fact]
    public void SearchText_FiltersCorrectly()
    {
        var vm = new ZonasProyectoViewModel();
        vm.AgregarZona("Subterráneo 1");
        vm.AgregarZona("Piso 1 Torre A");
        vm.AgregarZona("Piso 2 Torre A");
        vm.AgregarZona("Sala de Bombas");

        vm.SearchText = "Bombas";
        var filteredList = vm.FilteredRows.Cast<ZonaEditRowVm>().ToList();
        Assert.Single(filteredList);
        Assert.Equal("Sala de Bombas", filteredList[0].Nombre);
        Assert.Equal("Técnico", filteredList[0].TipoDisplay);

        vm.SearchText = "Torre";
        filteredList = vm.FilteredRows.Cast<ZonaEditRowVm>().ToList();
        Assert.Equal(2, filteredList.Count);

        vm.SearchText = string.Empty;
        filteredList = vm.FilteredRows.Cast<ZonaEditRowVm>().ToList();
        Assert.Equal(4, filteredList.Count);
    }

    [Fact]
    public void SugerirSiguienteNombreZona_AutoIncrementsCorrelativeFloorNumbers()
    {
        var vm = new ZonasProyectoViewModel();

        // 1. Sin zonas: sugiere Piso 1
        Assert.Equal("Piso 1", vm.SugerirSiguienteNombreZona());

        // 2. Si la última es Piso 1: sugiere Piso 2
        vm.AgregarZona("Piso 1");
        Assert.Equal("Piso 2", vm.SugerirSiguienteNombreZona());

        // 3. Si la última es Piso 2: sugiere Piso 3
        vm.AgregarZona("Piso 2");
        Assert.Equal("Piso 3", vm.SugerirSiguienteNombreZona());

        // 4. Subterráneo con signo negativo
        vm.AgregarZona("Subterráneo -1");
        Assert.Equal("Subterráneo -2", vm.SugerirSiguienteNombreZona());

        // 5. Nombres compuestos con prefijo
        vm.AgregarZona("Torre B - Nivel 4");
        Assert.Equal("Torre B - Nivel 5", vm.SugerirSiguienteNombreZona());
    }

    [Fact]
    public void GenerarRangoPisos_CreatesSequentialFloorsAccurately()
    {
        var vm = new ZonasProyectoViewModel();

        // Generar Pisos 1 al 5
        vm.GenerarRangoPisos("Piso ", 1, 5, reemplazarExistentes: true);

        Assert.Equal(5, vm.Rows.Count);
        Assert.Equal("Piso 1", vm.Rows[0].Nombre);
        Assert.Equal("Piso 2", vm.Rows[1].Nombre);
        Assert.Equal("Piso 3", vm.Rows[2].Nombre);
        Assert.Equal("Piso 4", vm.Rows[3].Nombre);
        Assert.Equal("Piso 5", vm.Rows[4].Nombre);

        // Generar Subterráneos -3 al -1 anexando
        vm.GenerarRangoPisos("Subterráneo ", -3, -1, reemplazarExistentes: false);

        Assert.Equal(8, vm.Rows.Count);
        Assert.Equal("Subterráneo -3", vm.Rows[5].Nombre);
        Assert.Equal("Subterráneo -2", vm.Rows[6].Nombre);
        Assert.Equal("Subterráneo -1", vm.Rows[7].Nombre);
    }

    [Fact]
    public void ObtenerZonasResultantes_ProducesOrderedList()
    {
        var vm = new ZonasProyectoViewModel();
        vm.AgregarZona("Subterráneo");
        vm.AgregarZona("Piso 1");

        var result = vm.ObtenerZonasResultantes();

        Assert.Equal(2, result.Count);
        Assert.Equal("Subterráneo", result[0].Nombre);
        Assert.Equal(0, result[0].Orden);
        Assert.Equal("Piso 1", result[1].Nombre);
        Assert.Equal(1, result[1].Orden);
    }
}
