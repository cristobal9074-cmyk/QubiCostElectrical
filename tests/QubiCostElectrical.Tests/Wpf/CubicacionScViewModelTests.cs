using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionScViewModelTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.CubicacionSc;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false) { CurrentRoute = route; return true; }
        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) => RequestNavigate(route, force);
        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }

    public CubicacionScViewModelTests() => WpfTestHost.EnsureInitialized();

    private static CubicacionScViewModel CreateWithProject(string projectName)
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject(projectName);
        host.Session.CurrentProjectPath = $"{projectName}.qce";
        return new CubicacionScViewModel(new FakeShell(), host)
        {
            ConfirmDeleteCircuito = _ => true
        };
    }

    [Fact]
    public void Tipos_ExposeLosCincoSistemasDeUso()
    {
        var vm = CreateWithProject("csc-tipos");

        Assert.Equal(new[] { "A", "E", "CL", "CO", "C", "F" }, vm.Tipos);
    }

    [Fact]
    public void Ubicacion_EscrituraLibre_SeGuardaDirecto()
    {
        var vm = CreateWithProject("csc-ubic-libre");
        vm.AddCircuitoCommand.Execute(null);
        var c = vm.SelectedCircuito!;

        c.Ubicacion = "-2";

        Assert.Equal("-2", c.Ubicacion);
        Assert.False(c.TienePisos);
    }

    [Fact]
    public void PopulateYCommitZonaPisos_ConLargos_ActivaVariosPisos()
    {
        var vm = CreateWithProject("csc-zonas");
        vm.AddCircuitoCommand.Execute(null);
        var c = vm.SelectedCircuito!;
        var zonas = new System.Collections.Generic.List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Piso 1", Orden = 1, Activa = true },
            new() { Id = "z2", Nombre = "Piso 2", Orden = 2, Activa = true },
        };

        c.PopulateZonaPisos(zonas);
        c.ZonaPisos[0].MetrosTuberia = 30m; // Piso 1
        c.ZonaPisos[1].MetrosBandeja = 15m; // Piso 2
        c.CommitZonaPisos();

        Assert.True(c.TienePisos);
        Assert.Equal("Piso 1, Piso 2", c.Ubicacion);
        Assert.Equal(30m, c.LargoTuberia);
        Assert.Equal(15m, c.LargoBandeja);
    }

    [Fact]
    public void ZonaPisosTotal_SumaLargosDeTodasLasZonas()
    {
        var vm = CreateWithProject("csc-total-zonas");
        vm.AddCircuitoCommand.Execute(null);
        var c = vm.SelectedCircuito!;
        var zonas = new System.Collections.Generic.List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Piso 1", Orden = 1, Activa = true },
            new() { Id = "z2", Nombre = "Piso 2", Orden = 2, Activa = true },
        };

        c.PopulateZonaPisos(zonas);
        Assert.Equal(0m, c.ZonaPisosTotal);

        c.ZonaPisos[0].MetrosBandeja = 10m;
        c.ZonaPisos[0].MetrosTuberia = 5m;
        c.ZonaPisos[1].MetrosTuberia = 20m;

        Assert.Equal(35m, c.ZonaPisosTotal);
    }

    [Fact]
    public void CommitZonaPisos_SinLargos_VuelveAModoTextoLibre()
    {
        var vm = CreateWithProject("csc-zonas-vacio");
        vm.AddCircuitoCommand.Execute(null);
        var c = vm.SelectedCircuito!;
        var zonas = new System.Collections.Generic.List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Piso 1", Orden = 1, Activa = true },
        };
        c.PopulateZonaPisos(zonas);
        c.ZonaPisos[0].MetrosTuberia = 10m;
        c.CommitZonaPisos();
        Assert.True(c.TienePisos);

        // El usuario borra el largo en el flyout → ninguna zona queda activa.
        c.PopulateZonaPisos(zonas);
        c.ZonaPisos[0].MetrosTuberia = 0m;
        c.CommitZonaPisos();

        Assert.False(c.TienePisos);
    }

    [Fact]
    public void Ubicacion_MientrasTienePisos_EsDeSoloLecturaEnElSetter()
    {
        var vm = CreateWithProject("csc-readonly");
        vm.AddCircuitoCommand.Execute(null);
        var c = vm.SelectedCircuito!;
        var zonas = new System.Collections.Generic.List<ZonaProyecto>
        {
            new() { Id = "z1", Nombre = "Piso 1", Orden = 1, Activa = true },
        };
        c.PopulateZonaPisos(zonas);
        c.ZonaPisos[0].MetrosTuberia = 10m;
        c.CommitZonaPisos();

        c.Ubicacion = "intento manual"; // se ignora mientras TienePisos sea true

        Assert.Equal("Piso 1", c.Ubicacion);
    }

    [Fact]
    public void Circuito_AlConstruir_SincronizaConductoresYMermaConLaAlim()
    {
        // Un circuito guardado con el valor legacy 3,2 debe re-sincronizarse a lo que dicta su
        // alim en el catálogo apenas se carga (no quedarse en 3,2).
        var row = new CircuitoSCRow { Sistema = "T", NumConductores = 3.2m, MermaPct = 0m };

        var vm = new CircuitoScRowVm(row, CubicacionScConfig.Default(), Array.Empty<ZonaProyecto>());

        Assert.Equal(5m, vm.NumConductores); // T → 5 conductores
        Assert.Equal(8m, vm.MermaPct);       // T → 8% de merma
    }

    [Fact]
    public void Ubicacion_AlEscribirRango_ParseaYRegistraPisosTramos()
    {
        var row = new CircuitoSCRow { Ubicacion = "" };
        var config = CubicacionScConfig.Default();
        var zonas = new[]
        {
            new ZonaProyecto { Id = "z1", Nombre = "Subterráneo 7", Orden = 1, Activa = true },
            new ZonaProyecto { Id = "z2", Nombre = "Subterráneo 6", Orden = 2, Activa = true },
            new ZonaProyecto { Id = "z3", Nombre = "Piso 1", Orden = 3, Activa = true },
            new ZonaProyecto { Id = "z4", Nombre = "Piso 2", Orden = 4, Activa = true }
        };

        var vm = new CircuitoScRowVm(row, config, zonas);
        vm.Ubicacion = "S7 al P1";

        Assert.True(row.TienePisos);
        Assert.Equal(3, row.PisosTramos.Count);
        Assert.Equal("z1", row.PisosTramos[0].ZonaId);
        Assert.Equal("z2", row.PisosTramos[1].ZonaId);
        Assert.Equal("z3", row.PisosTramos[2].ZonaId);
    }

    [Fact]
    public void Fase_AlCambiar_AplicaNumConductoresYMermaDelSistema()
    {
        var vm = CreateWithProject("csc-fase");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;

        circuito.Fase = "T";

        Assert.Equal(5m, circuito.NumConductores);
        Assert.Equal(8m, circuito.MermaPct);
    }

    // Regresión: pedido del usuario de poder escribir rápido a puro teclado en Sist./Alim./
    // Ducto Ø (antes ComboBox, ahora texto libre — ver CubicacionScView.xaml). Minúscula o
    // mayúscula, con espacios alrededor, debe normalizar al código válido.
    [Theory]
    [InlineData("e", "E")]
    [InlineData("E", "E")]
    [InlineData(" cl ", "CL")]
    [InlineData("co", "CO")]
    [InlineData("CO", "CO")]
    [InlineData("computacion", "CO")]
    [InlineData("Alumbrado", "A")]
    [InlineData("f", "F")]
    public void Tipo_TextoLibre_NormalizaACodigoValido(string escrito, string esperado)
    {
        var vm = CreateWithProject("csc-tipo-texto-libre");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;

        circuito.Tipo = escrito;

        Assert.Equal(esperado, circuito.Tipo);
    }

    [Fact]
    public void Tipo_TextoInvalido_SeDescartaYMantieneElValorAnterior()
    {
        var vm = CreateWithProject("csc-tipo-invalido");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;
        circuito.Tipo = "E";

        circuito.Tipo = "zzz";

        Assert.Equal("E", circuito.Tipo);
    }

    [Fact]
    public void Fase_TextoLibreMinuscula_NormalizaYAplicaSistema()
    {
        var vm = CreateWithProject("csc-fase-texto-libre");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;

        circuito.Fase = "t";

        Assert.Equal("T", circuito.Fase);
        Assert.Equal(5m, circuito.NumConductores);
        Assert.Equal(8m, circuito.MermaPct);
    }

    [Fact]
    public void SeccionTuberia_SoloNumero_NormalizaAgregandoMm()
    {
        var vm = CreateWithProject("csc-ducto-numero");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;

        circuito.SeccionTuberia = "32";

        Assert.Equal("32mm", circuito.SeccionTuberia);
    }

    [Fact]
    public void SeccionTuberia_25_NormalizaA25mm_YNoA50mm()
    {
        var vm = CreateWithProject("csc-ducto-25");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;

        circuito.SeccionTuberia = "25";

        Assert.Equal("25mm", circuito.SeccionTuberia);
    }

    [Theory]
    [InlineData("25", "25mm")]
    [InlineData("20", "20mm")]
    [InlineData("16", "16mm")]
    [InlineData("32", "32mm")]
    [InlineData("50", "50mm")]
    public void FindBestCatalogMatch_DiametrosTuberia(string input, string esperado)
    {
        var catalogo = new[] { "16mm", "20mm", "25mm", "32mm", "40mm", "50mm", "63mm" };
        var res = QubiCostElectrical.Wpf.Features.CubicacionSc.CubicacionScView.FindBestCatalogMatch(catalogo, input);
        Assert.Equal(esperado, res);
    }

    [Theory]
    [InlineData("25", "25mm2")]
    [InlineData("2.5", "2.5mm2")]
    [InlineData("2,5", "2.5mm2")]
    [InlineData("1.5", "1.5mm2")]
    [InlineData("4", "4mm2")]
    [InlineData("50", "50mm2")]
    public void FindBestCatalogMatch_SeccionesConductor(string input, string esperado)
    {
        var catalogo = new[] { "1.5mm2", "2.5mm2", "4mm2", "6mm2", "10mm2", "16mm2", "25mm2", "35mm2", "50mm2" };
        var res = QubiCostElectrical.Wpf.Features.CubicacionSc.CubicacionScView.FindBestCatalogMatch(catalogo, input);
        Assert.Equal(esperado, res);
    }

    [Theory]
    [InlineData("emt", "EMT")]
    [InlineData("EMT", "EMT")]
    [InlineData("ca", "CAÑERIA")]
    [InlineData("pvc", "PVC")]
    public void FindBestCatalogMatch_TiposTuberia(string input, string esperado)
    {
        var catalogo = new[] { "PVC", "EMT", "CAÑERIA", "PVCLH" };
        var res = QubiCostElectrical.Wpf.Features.CubicacionSc.CubicacionScView.FindBestCatalogMatch(catalogo, input);
        Assert.Equal(esperado, res);
    }

    [Theory]
    [InlineData("eva", "EVA")]
    [InlineData("evan", "EVANOTOX")]
    [InlineData("thhn", "THHN")]
    [InlineData("nya", "NYA")]
    public void FindBestCatalogMatch_TiposConductor(string input, string esperado)
    {
        var catalogo = new[] { "NYA", "H07V-U", "SUPERFLEX", "THHN", "RVK", "XTU", "EVA", "EVANOTOX", "RZ1-K" };
        var res = QubiCostElectrical.Wpf.Features.CubicacionSc.CubicacionScView.FindBestCatalogMatch(catalogo, input);
        Assert.Equal(esperado, res);
    }

    // Regresión: agregar un artefacto que YA existe en el catálogo del proyecto debe heredar
    // su categoría; agregar uno nuevo debe registrarlo en el catálogo (con categoría "Otro")
    // para que quede disponible la próxima vez, sin duplicar la entrada si se repite.
    [Fact]
    public void AddArtefacto_ConNombreExistenteEnCatalogo_HeredaCategoria()
    {
        var vm = CreateWithProject("csc-artefacto-existente");
        vm.AddCircuitoCommand.Execute(null);

        vm.NuevoArtefactoNombre = "INT 9/12";
        vm.NuevoArtefactoCantidad = 3;
        vm.AddArtefactoCommand.Execute(null);

        var artefacto = Assert.Single(vm.SelectedCircuito!.Artefactos);
        Assert.Equal("INT 9/12", artefacto.Nombre);
        Assert.Equal("Interruptor", artefacto.Categoria);
        Assert.Equal(3m, artefacto.Cantidad);
    }

    [Fact]
    public void AddArtefacto_ConNombreNuevo_LoRegistraEnElCatalogoDelProyecto()
    {
        var vm = CreateWithProject("csc-artefacto-nuevo");
        vm.AddCircuitoCommand.Execute(null);

        vm.NuevoArtefactoNombre = "Extractor de baño";
        vm.AddArtefactoCommand.Execute(null);

        var artefacto = Assert.Single(vm.SelectedCircuito!.Artefactos);
        Assert.Equal("Otro", artefacto.Categoria);
        Assert.Contains("Extractor de baño", vm.ArtefactosCatalogo);
    }

    [Fact]
    public void AddArtefacto_LimpiaElFormularioDeCaptura()
    {
        var vm = CreateWithProject("csc-artefacto-limpia");
        vm.AddCircuitoCommand.Execute(null);

        vm.NuevoArtefactoNombre = "Sensor de movimiento";
        vm.NuevoArtefactoCantidad = 2;
        vm.AddArtefactoCommand.Execute(null);

        Assert.Equal("", vm.NuevoArtefactoNombre);
        Assert.Equal(1m, vm.NuevoArtefactoCantidad);
    }

    [Fact]
    public void RemoveArtefacto_LoQuitaDeLaListaDelCircuito()
    {
        var vm = CreateWithProject("csc-artefacto-quitar");
        vm.AddCircuitoCommand.Execute(null);
        vm.NuevoArtefactoNombre = "ENCH S10";
        vm.AddArtefactoCommand.Execute(null);
        var artefacto = vm.SelectedCircuito!.Artefactos[0];

        vm.RemoveArtefactoCommand.Execute(artefacto);

        Assert.Empty(vm.SelectedCircuito.Artefactos);
    }

    [Fact]
    public void TipoRecorrido_PorDefecto_EsTuberia()
    {
        var vm = CreateWithProject("csc-recorrido-default");
        vm.AddCircuitoCommand.Execute(null);

        Assert.True(vm.SelectedCircuito!.EsPorTuberia);
        Assert.False(vm.SelectedCircuito.EsPorBandeja);
    }

    [Fact]
    public void TipoRecorrido_CambiadoABandeja_ActualizaFlags()
    {
        var vm = CreateWithProject("csc-recorrido-bandeja");
        vm.AddCircuitoCommand.Execute(null);

        vm.SelectedCircuito!.TipoRecorrido = "Bandeja";

        Assert.False(vm.SelectedCircuito.EsPorTuberia);
        Assert.True(vm.SelectedCircuito.EsPorBandeja);
    }

    // Regresión: el contador "X / Y circuitos" del panel de tableros (CircuitosActivos)
    // debe reflejar en vivo los circuitos agregados, quitados o con Estado cambiado —
    // antes no se notificaba y quedaba desactualizado en la UI.
    [Fact]
    public void CircuitosActivos_SeActualizaAlAgregarCircuito()
    {
        var vm = CreateWithProject("csc-contador-agregar");
        var cuadro = vm.Cuadros[0];
        var cambios = 0;
        cuadro.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(cuadro.CircuitosActivos)) cambios++; };

        vm.AddCircuitoCommand.Execute(null);

        Assert.Equal(1, cuadro.CircuitosActivos);
        Assert.True(cambios > 0);
    }

    [Fact]
    public void CircuitosActivos_SeActualizaAlQuitarCircuito()
    {
        var vm = CreateWithProject("csc-contador-quitar");
        vm.AddCircuitoCommand.Execute(null);
        var cuadro = vm.Cuadros[0];
        Assert.Equal(1, cuadro.CircuitosActivos);

        vm.RemoveCircuitoCommand.Execute(null);

        Assert.Equal(0, cuadro.CircuitosActivos);
    }

    [Fact]
    public void CircuitosActivos_SeActualizaAlCambiarEstadoDeUnCircuito()
    {
        var vm = CreateWithProject("csc-contador-estado");
        vm.AddCircuitoCommand.Execute(null);
        var cuadro = vm.Cuadros[0];
        Assert.Equal(1, cuadro.CircuitosActivos);

        vm.SelectedCircuito!.Estado = "Reserva equipada";

        Assert.Equal(0, cuadro.CircuitosActivos);
    }

    [Fact]
    public void CircuitosActivos_NoCuentaCircuitosInactivos()
    {
        var vm = CreateWithProject("csc-contador-mixto");
        vm.AddCircuitoCommand.Execute(null);
        vm.SelectedCircuito!.Estado = "Reserva equipada";
        vm.AddCircuitoCommand.Execute(null);

        Assert.Equal(1, vm.Cuadros[0].CircuitosActivos);
    }

    [Fact]
    public void AddArtefacto_ConMismoNombreDosVeces_NoDuplicaEntradaEnCatalogo()
    {
        var vm = CreateWithProject("csc-catalogo-no-duplica");
        vm.AddCircuitoCommand.Execute(null);

        vm.NuevoArtefactoNombre = "Extractor de baño";
        vm.AddArtefactoCommand.Execute(null);
        vm.NuevoArtefactoNombre = "Extractor de baño";
        vm.AddArtefactoCommand.Execute(null);

        Assert.Single(vm.ArtefactosCatalogo, n => n == "Extractor de baño");
        Assert.Equal(2, vm.SelectedCircuito!.Artefactos.Count);
    }

    [Fact]
    public void AddArtefacto_NombreConDistintasMayusculas_CoincideConCatalogoExistente()
    {
        var vm = CreateWithProject("csc-catalogo-case-insensitive");
        vm.AddCircuitoCommand.Execute(null);

        vm.NuevoArtefactoNombre = "int 9/12";
        vm.AddArtefactoCommand.Execute(null);

        var artefacto = Assert.Single(vm.SelectedCircuito!.Artefactos);
        Assert.Equal("Interruptor", artefacto.Categoria);
    }

    [Fact]
    public void RemoveArtefacto_ConVariosDelMismoNombre_QuitaSoloUnaInstancia()
    {
        var vm = CreateWithProject("csc-artefacto-quitar-una-instancia");
        vm.AddCircuitoCommand.Execute(null);
        vm.NuevoArtefactoNombre = "ENCH S10";
        vm.AddArtefactoCommand.Execute(null);
        vm.NuevoArtefactoNombre = "ENCH S10";
        vm.AddArtefactoCommand.Execute(null);
        var primero = vm.SelectedCircuito!.Artefactos[0];

        vm.RemoveArtefactoCommand.Execute(primero);

        Assert.Single(vm.SelectedCircuito.Artefactos);
    }

    [Fact]
    public void AddCircuito_NumeroSiguienteConsideraElMaximoAunTrasEliminarUno()
    {
        var vm = CreateWithProject("csc-numero-tras-eliminar");
        vm.AddCircuitoCommand.Execute(null);
        vm.AddCircuitoCommand.Execute(null);
        vm.AddCircuitoCommand.Execute(null);
        vm.RemoveCircuitoCommand.Execute(null);

        vm.AddCircuitoCommand.Execute(null);

        Assert.Equal(3, vm.SelectedCircuito!.Numero);
    }

    [Fact]
    public void AddCircuito_EnDeptos_A01NoQuedaFijoEnUnoYSePuedeEditar()
    {
        var vm = CreateWithProject("csc-depto-a01");
        vm.SelectPerspective("Deptos");
        vm.AddCuadroCommand.Execute(null);

        vm.AddCircuitoCommand.Execute(null);
        Assert.Equal(0m, vm.SelectedCircuito!.Cajas100x65);

        vm.SelectedCircuito.Cajas100x65 = 3m;
        Assert.Equal(3m, vm.SelectedCircuito.Cajas100x65);

        vm.AddCircuitoCommand.Execute(null);
        Assert.Equal(0m, vm.SelectedCircuito!.Cajas100x65);
    }

    [Fact]
    public void AddCircuito_EnGenerales_A01SigueEnUno()
    {
        var vm = CreateWithProject("csc-gen-a01");
        vm.AddCircuitoCommand.Execute(null);

        Assert.Equal(1m, vm.SelectedCircuito!.Cajas100x65);
    }

    [Fact]
    public void AddCircuito_HeredaConductorDelCircuitoActivo()
    {
        var vm = CreateWithProject("csc-hereda-cond");
        vm.AddCircuitoCommand.Execute(null);
        vm.SelectedCircuito!.ConductorDistribucion = "NYA";
        vm.SelectedCircuito.SeccionDistribucion = "2.5mm2";
        vm.SelectedCircuito.ConductorDerivacion = "THHN";

        vm.AddCircuitoCommand.Execute(null);

        Assert.Equal("NYA", vm.SelectedCircuito!.ConductorDistribucion);
        Assert.Equal("2.5mm2", vm.SelectedCircuito.SeccionDistribucion);
        Assert.Equal("THHN", vm.SelectedCircuito.ConductorDerivacion);
        Assert.Equal(0m, vm.SelectedCircuito.LargoTuberia); // REC no se hereda
    }

    [Fact]
    public void DuplicateCircuito_CopiaConductorArtefactosYPisosConNumeroNuevo()
    {
        var vm = CreateWithProject("csc-dup");
        vm.AddCircuitoCommand.Execute(null);
        var original = vm.SelectedCircuito!;
        original.ConductorDistribucion = "NYA";
        original.SeccionDistribucion = "2.5mm2";
        original.ConductorDerivacion = "H07V-U";
        original.LargoTuberia = 40m;
        original.AddArtefacto("ENCH S10", "Enchufe", 3);
        string originalId = original.Id;
        int originalNumero = original.Numero;

        vm.DuplicateCircuitoCommand.Execute(null);

        var copy = vm.SelectedCircuito!;
        Assert.NotEqual(originalId, copy.Id);
        Assert.Equal(originalNumero + 1, copy.Numero);
        Assert.Equal("NYA", copy.ConductorDistribucion);
        Assert.Equal("2.5mm2", copy.SeccionDistribucion);
        Assert.Equal("H07V-U", copy.ConductorDerivacion);
        Assert.Equal(40m, copy.LargoTuberia);
        Assert.Single(copy.Artefactos);
        Assert.Equal("ENCH S10", copy.Artefactos[0].Nombre);
        Assert.Equal(3m, copy.Artefactos[0].Cantidad);
        Assert.Equal(2, vm.Circuitos.Count);
    }

    [Fact]
    public void CopyPasteCircuito_ReemplazaElDestinoSinAgregarFila()
    {
        var vm = CreateWithProject("csc-copy-paste");
        vm.AddCircuitoCommand.Execute(null);
        vm.SelectedCircuito!.ConductorDistribucion = "THHN";
        vm.SelectedCircuito.AddArtefacto("INT 9/12", "Interruptor", 1);
        var origenId = vm.SelectedCircuito.Id;

        vm.CopyCircuitoCommand.Execute(null);

        vm.AddCircuitoCommand.Execute(null);
        var destino = vm.SelectedCircuito!;
        destino.ConductorDistribucion = "RVK";
        int destinoNumero = destino.Numero;
        string destinoId = destino.Id;
        Assert.Equal(2, vm.Circuitos.Count);

        vm.PasteCircuitoCommand.Execute(null);

        Assert.Equal(2, vm.Circuitos.Count); // no agrega fila
        Assert.Equal(destinoId, vm.SelectedCircuito!.Id); // mismo circuito
        Assert.Equal(destinoNumero, vm.SelectedCircuito.Numero);
        Assert.Equal("THHN", vm.SelectedCircuito.ConductorDistribucion);
        Assert.Single(vm.SelectedCircuito.Artefactos);
        Assert.Equal("INT 9/12", vm.SelectedCircuito.Artefactos[0].Nombre);
        Assert.Equal("THHN", vm.Circuitos.First(c => c.Id == origenId).ConductorDistribucion);
    }

    [Fact]
    public void RemoveCuadro_CuandoEsElUltimo_DejaListaDeCircuitosVacia()
    {
        var vm = CreateWithProject("csc-remove-ultimo-cuadro");
        vm.ConfirmDelete = _ => true;

        vm.RemoveCuadroCommand.Execute(null);

        Assert.Null(vm.SelectedCuadro);
        Assert.Empty(vm.Circuitos);
    }

    [Fact]
    public void TiposConductorYSeccionesConductor_ExponenElCatalogoCompleto()
    {
        var vm = CreateWithProject("csc-catalogos-conductor");

        Assert.Contains("H07V-U", vm.TiposConductor);
        Assert.Contains("4mm2", vm.SeccionesConductor);
    }

    [Fact]
    public void AltA_ConVariosPisos_MuestraSoloLosDelCircuito()
    {
        var zonas = new[]
        {
            new ZonaProyecto { Id = "z1", Nombre = "Piso 1", Orden = 1 },
            new ZonaProyecto { Id = "z2", Nombre = "Piso 2", Orden = 2 },
            new ZonaProyecto { Id = "z3", Nombre = "Azotea", Orden = 3 }
        };
        var circuito = new CircuitoSCRow
        {
            PisosTramos =
            {
                new CircuitoPisoTramo { ZonaId = "z1", ZonaNombre = "Piso 1" },
                new CircuitoPisoTramo { ZonaId = "z3", ZonaNombre = "Azotea" }
            }
        };

        var pisos = CubicacionScArtefactosDialog.ResolverPisos(circuito, zonas, Array.Empty<string>());

        Assert.Equal(new[] { "Piso 1", "Azotea" }, pisos.Select(p => p.Nombre).ToArray());
    }

    [Fact]
    public void AltA_ConUnPiso_NoAgregaElRestoDeZonas()
    {
        var zonas = new[]
        {
            new ZonaProyecto { Id = "z1", Nombre = "Piso 1" },
            new ZonaProyecto { Id = "z2", Nombre = "Piso 2" }
        };
        var circuito = new CircuitoSCRow { Ubicacion = "Piso 2" };

        var pisos = CubicacionScArtefactosDialog.ResolverPisos(circuito, zonas, Array.Empty<string>());

        Assert.Equal(("z2", "Piso 2"), Assert.Single(pisos));
    }

    [Fact]
    public void Resumen_TrasAutoguardado_MuestraArtefactosQueSiguenEnLaGrilla()
    {
        var host = WpfAppHost.Current;
        var vm = CreateWithProject("csc-resumen-artefactos");
        vm.AddCircuitoCommand.Execute(null);
        vm.SelectedCircuito!.AddArtefacto("INT 9/12", "Interruptor", 4m);
        vm.SelectedCircuito.AddArtefacto("ENCH S10", "Enchufe", 2m);

        vm.SetSessionChangeActive(false);
        try
        {
            var snapshot = host.Session.CreateSnapshot();
            foreach (var circuito in snapshot.CubicacionSC.SelectMany(c => c.Circuitos))
                circuito.Artefactos.Clear();
            host.Session.ApplySnapshot(snapshot, host.Session.CurrentProjectPath, markDirty: false);

            vm.SelectedCircuito.Cajas58 = 100m;
            vm.SelectedCircuito.Cajas100x100 = 100m;
            vm.SelectedCircuito.Cajas100x65 = 100m;

            Assert.Empty(host.Session.CubicacionSC.SelectMany(c => c.Circuitos).SelectMany(c => c.Artefactos));
            Assert.Equal(0m, host.Session.CubicacionSC.SelectMany(c => c.Circuitos).Single().Cajas58);
            Assert.Equal(2, vm.SelectedCircuito.Source.Artefactos.Count);
            Assert.True(vm.SincronizarGrillaConSesion());

            var total = CubicacionSCEngine.AggregateTotal(host.Session.CubicacionSC, host.Session.CubicacionScConfig);
            Assert.Equal(6, total.Artefactos);
            Assert.Equal(300, total.Cajas);
            Assert.Contains(total.Materiales, m => m.Nombre == "INT 9/12" && m.Cantidad == 4m);
            Assert.Contains(total.Materiales, m => m.Nombre == "ENCH S10" && m.Cantidad == 2m);
            Assert.Contains(total.Materiales, m => m.Nombre.Contains("5/8", StringComparison.Ordinal) && m.Cantidad == 100m);
            Assert.Contains(total.Materiales, m => m.Nombre.Contains("100X100", StringComparison.OrdinalIgnoreCase) && m.Cantidad == 100m);
            Assert.Contains(total.Materiales, m => m.Nombre.Contains("100X65", StringComparison.OrdinalIgnoreCase) && m.Cantidad == 100m);
        }
        finally
        {
            vm.SetSessionChangeActive(true);
        }
    }

    [Fact]
    public void Resumen_AgrupaCajasYNoSumaTuberiaConConductor()
    {
        var lineas = ResumenScPresentacion.Agrupar(
        [
            new CubicacionSCEngine.MaterialSC("TUBERIA PVCLH 20mm", 10m, "ML"),
            new CubicacionSCEngine.MaterialSC("CONDUCTOR H07V-U 4mm2", 32m, "ML"),
            new CubicacionSCEngine.MaterialSC("CAJA 5/8", 100m, "UN"),
            new CubicacionSCEngine.MaterialSC("CAJA A11 (100X100)", 100m, "UN"),
            new CubicacionSCEngine.MaterialSC("CAJA A01 (100X65)", 100m, "UN"),
            new CubicacionSCEngine.MaterialSC("INT 9/12", 10m, "UN"),
            new CubicacionSCEngine.MaterialSC("MONTAJE LUMINARIA", 100m, "UN")
        ], "");

        Assert.Contains(lineas, l => l.EsGrupo && l.Nombre == "CANALIZACIÓN");
        Assert.DoesNotContain(lineas, l => l.EsSubtotal && l.Nombre.Contains("canalización", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lineas, l => l.EsSubtotal && l.Nombre.Contains("cajas", StringComparison.OrdinalIgnoreCase) && l.Cantidad == ResumenScPresentacion.Fmt(300m));
        Assert.Contains(lineas, l => l.EsSubtotal && l.Nombre.Contains("artefactos", StringComparison.OrdinalIgnoreCase) && l.Cantidad == ResumenScPresentacion.Fmt(110m));

        var filtradas = ResumenScPresentacion.Agrupar(
        [
            new CubicacionSCEngine.MaterialSC("CAJA 5/8", 100m, "UN"),
            new CubicacionSCEngine.MaterialSC("INT 9/12", 10m, "UN")
        ], "caja");
        Assert.Single(filtradas, l => l.Nombre == "CAJA 5/8");
        Assert.DoesNotContain(filtradas, l => l.Nombre == "INT 9/12");
    }

    [Fact]
    public void AltA_Cantidad_AceptaOperaciones()
    {
        Assert.True(CubicacionScArtefactosDialog.TryEvaluarCantidad("13+5+6+5", out var suma));
        Assert.Equal(29m, suma);
        Assert.True(CubicacionScArtefactosDialog.TryEvaluarCantidad("2*4", out var producto));
        Assert.Equal(8m, producto);
        Assert.True(CubicacionScArtefactosDialog.TryEvaluarCantidad("12", out var plano));
        Assert.Equal(12m, plano);
    }

    [Fact]
    public void RemoveCircuito_WhenConfirmed_RemovesCircuitoAndUpdatesTotals()
    {
        var vm = CreateWithProject("csc-delete-confirmed");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;
        circuito.Numero = 101;
        bool promptCalled = false;
        vm.ConfirmDeleteCircuito = msg =>
        {
            promptCalled = true;
            Assert.Contains("101", msg);
            return true;
        };

        vm.RemoveCircuitoCommand.Execute(null);

        Assert.True(promptCalled);
        Assert.DoesNotContain(vm.Circuitos, c => c.Numero == 101);
    }

    [Fact]
    public void RemoveCircuito_WhenCancelled_KeepsCircuitoIntact()
    {
        var vm = CreateWithProject("csc-delete-cancelled");
        vm.AddCircuitoCommand.Execute(null);
        var circuito = vm.SelectedCircuito!;
        circuito.Numero = 102;
        bool promptCalled = false;
        vm.ConfirmDeleteCircuito = msg =>
        {
            promptCalled = true;
            return false; // Cancela la eliminación
        };

        vm.RemoveCircuitoCommand.Execute(null);

        Assert.True(promptCalled);
        Assert.Contains(vm.Circuitos, c => c.Numero == 102);
    }
}
