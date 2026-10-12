using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Costo;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Serializado con el resto de pruebas que usan las preferencias globales: el modo automático del
/// FA se guarda ahí y el archivo real del usuario no debe tocarse desde los tests.
/// </summary>
[Collection("WpfHost")]
public sealed class MoInstalacionSectionVmTests : IDisposable
{
    private readonly IDisposable prefsScope = UserPreferencesStore.UsePreferencesFilePathForTests(
        Path.Combine(Path.GetTempPath(), $"prefs_fa_{Guid.NewGuid():N}", "preferences.json"));

    public void Dispose() => prefsScope.Dispose();

    private static MoInstalacionSectionVm CrearVm(BudgetProjectSession session, bool faAutomatico = false)
    {
        var vm = new MoInstalacionSectionVm(session,
            canEdit: () => true,
            moDirectaRows: () => Array.Empty<MoDirectaRowVm>(),
            runSuppressed: action => action(),
            refreshCost: () => { });
        vm.FaAutomatico = faAutomatico;
        return vm;
    }

    [Fact]
    public void FaAviso_SinPlanDeManoDeObra_AvisaQueElFaEsGenerico()
    {
        var vm = CrearVm(new BudgetProjectSession());

        vm.Load(new ProjectInfoRow());

        Assert.True(vm.FaAvisoVisible);
        Assert.Contains("Sin plan de mano de obra", vm.FaAviso);
    }

    [Fact]
    public void FaAviso_ConPlanDeManoDeObra_NoAvisa()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.MoTotalClp = 10_000_000m;
        session.Materiales.Add(new MaterialRow
        {
            Codigo = "PCT-01",
            MatClp = 50_000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        var vm = CrearVm(session);

        vm.Load(session.ProjectInfo);

        Assert.False(vm.FaAvisoVisible);
        Assert.Contains("Calcular FA", vm.FaResultado);
    }

    [Fact]
    public void CalcularFa_DerivaElIndiceYLoPersisteEnElProyecto()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.MoTotalClp = 10_000_000m;
        session.Materiales.Add(new MaterialRow { Codigo = "DIR-01", MoClp = 1_000m });
        session.Cubicacion.Add(new CubicacionRow { Codigo = "DIR-01", Cantidad = 100m });
        session.Materiales.Add(new MaterialRow
        {
            Codigo = "PCT-01",
            MatClp = 50_000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        session.Cubicacion.Add(new CubicacionRow { Codigo = "PCT-01", Cantidad = 100m });
        var vm = CrearVm(session);
        vm.Load(session.ProjectInfo);

        vm.CalcularFaCommand.Execute(null);

        // FA = (10.000.000 − 100.000) ÷ 125.000 = 79,20
        Assert.Equal(79.20m, session.ProjectInfo.IndiceMoFa);
        Assert.StartsWith("M.O. real", vm.FaResultado);
        Assert.False(vm.FaAvisoVisible);
    }

    [Fact]
    public void CalcularFa_SinPlanDeManoDeObra_NoTocaElIndice()
    {
        var session = new BudgetProjectSession();
        var vm = CrearVm(session);
        vm.Load(session.ProjectInfo);
        decimal antes = session.ProjectInfo.IndiceMoFa;

        vm.CalcularFaCommand.Execute(null);

        Assert.Equal(antes, session.ProjectInfo.IndiceMoFa);
        Assert.Contains("no tiene plan de mano de obra", vm.FaResultado);
    }

    /// <summary>Sin pulsar nada, el FA derivado del plan de M.O. ya se muestra frente al vigente.</summary>
    [Fact]
    public void FaDerivado_MuestraElFaDelPlanSinPulsarCalcular()
    {
        var (_, vm) = SesionCalibrable();

        Assert.True(vm.FaDerivadoVisible);
        Assert.Contains(N2(79.20m), vm.FaDerivadoTexto);
        Assert.Contains(N2(0m), vm.FaDerivadoTexto);
        Assert.Contains("Calcular FA", vm.FaDerivadoTexto);
    }

    /// <summary>
    /// El bloque FACTORES replica el Excel (COSTOS!F192 vs G192): la M.O. que el itemizado deja
    /// "EN PPTO" con el FA vigente contra el "COSTO" real del plan de obra.
    /// </summary>
    [Fact]
    public void Factores_ComparaLaMoDelItemizadoConElCostoRealDelPlan()
    {
        var (_, vm) = SesionCalibrable();

        // Proyecto sin FA calibrado: la M.O. por % aporta 0, así que EN PPTO solo tiene la M.O. fija
        // (1.000 × 100 filas = 100.000) contra los 10.000.000 del plan de obra.
        Assert.Equal(CurrencyFormat.Clp(100_000m), vm.MoEnPptoLabel);
        Assert.Equal(CurrencyFormat.Clp(10_000_000m), vm.MoCostoRealLabel);
        Assert.True(vm.FactoresTieneCostoReal);
        Assert.True(vm.MoDeltaEsAviso);
    }

    /// <summary>Al calibrar el FA, la brecha del bloque FACTORES queda en cero: el itemizado cuadra con la obra.</summary>
    [Fact]
    public void Factores_TrasCalibrarElFa_LaMoDelItemizadoCuadraConElCostoReal()
    {
        var (_, vm) = SesionCalibrable();

        vm.CalcularFaCommand.Execute(null);

        Assert.Equal(CurrencyFormat.Clp(10_000_000m), vm.MoEnPptoLabel);
        Assert.False(vm.MoDeltaEsAviso);
        Assert.StartsWith("0", vm.MoDeltaLabel);
    }

    /// <summary>Sin plan de M.O. no hay contra qué comparar: la columna COSTO avisa con "—".</summary>
    [Fact]
    public void Factores_SinPlanDeManoDeObra_NoMuestraCostoReal()
    {
        var vm = CrearVm(new BudgetProjectSession());
        vm.Load(new ProjectInfoRow());

        Assert.Equal("—", vm.MoCostoRealLabel);
        Assert.False(vm.FactoresTieneCostoReal);
        Assert.False(vm.MoDeltaEsAviso);
    }

    /// <summary>
    /// Días-hombre y tarifa implícita del Excel: dotación 100 + 82 = 182 × 22 = 4.004 DD/HH y
    /// 171.990.000 ÷ 4.004 = 42.955 por día-hombre (MANO DE OBRA!U23 y U4 del archivo original).
    /// </summary>
    [Fact]
    public void Factores_MuestraDiasHombreYTarifaImplicitaDelPlan()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.MoTotalClp = 171_990_000m;
        session.Data.MoDirecta.Add(new MoDirectaRow { Dotacion = 100, DotacionMensual = new List<int> { 100 } });
        session.Data.MoDirecta.Add(new MoDirectaRow { Dotacion = 82, DotacionMensual = new List<int> { 82 } });
        var vm = CrearVm(session);
        vm.Load(session.ProjectInfo);

        Assert.Equal(4_004m.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), vm.MoDiasHombreLabel);
        Assert.Equal(CurrencyFormat.Clp(42_955m) + " /D-H", vm.MoTarifaLabel);
    }

    private static string N2(decimal valor) =>
        valor.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

    [Fact]
    public void FaDerivado_TrasCalibrar_QuedaCuadrado()
    {
        var (_, vm) = SesionCalibrable();

        vm.CalcularFaCommand.Execute(null);

        Assert.Contains("FA calibrado", vm.FaDerivadoTexto);
    }

    [Fact]
    public void FaDerivado_SinPlanDeManoDeObra_NoSeMuestra()
    {
        var vm = CrearVm(new BudgetProjectSession());
        vm.Load(new ProjectInfoRow());

        Assert.False(vm.FaDerivadoVisible);
    }

    /// <summary>
    /// Editar el índice a mano recalibra la M.O. guardada del catálogo y las filas de cubicación:
    /// el motor, la lista de precios y el itemizado tienen que quedar con la misma cifra.
    /// </summary>
    [Fact]
    public void IndiceMoFaText_EditadoAMano_RecalibraCatalogoYCubicacion()
    {
        var (session, vm) = SesionCalibrable();

        vm.IndiceMoFaText = N2(79.20m);

        Assert.Equal(79.20m, session.ProjectInfo.IndiceMoFa);
        // 50.000 × 2,5 % × 79,20 = 99.000
        var material = session.Materiales.First(m => m.Codigo == "PCT-01");
        Assert.Equal(99_000m, material.MoClp);
        Assert.Equal(99_000m, session.Cubicacion.First(r => r.Codigo == "PCT-01").MoClp);
    }

    [Fact]
    public void IndiceMoFaText_Invalido_NoTocaElIndice()
    {
        var (session, vm) = SesionCalibrable();

        vm.IndiceMoFaText = "abc";

        Assert.Equal(0m, session.ProjectInfo.IndiceMoFa);
    }

    /// <summary>Proyecto calibrable: M.O. real 10.000.000 y base de porcentaje 125.000 → FA 79,20.</summary>
    private static (BudgetProjectSession Session, MoInstalacionSectionVm Vm) SesionCalibrable(bool faAutomatico = false)
    {
        var session = SesionCalibrableBase();
        var vm = CrearVm(session, faAutomatico);
        vm.Load(session.ProjectInfo);
        return (session, vm);
    }

    private static BudgetProjectSession SesionCalibrableBase()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.MoTotalClp = 10_000_000m;
        session.Materiales.Add(new MaterialRow { Codigo = "DIR-01", MoClp = 1_000m });
        session.Cubicacion.Add(new CubicacionRow { Codigo = "DIR-01", Cantidad = 100m });
        session.Materiales.Add(new MaterialRow
        {
            Codigo = "PCT-01",
            MatClp = 50_000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        session.Cubicacion.Add(new CubicacionRow { Codigo = "PCT-01", Cantidad = 100m });
        return session;
    }

    // ── Modo automático: el FA se cuadra solo contra el costo real ────────────

    /// <summary>
    /// Con el modo automático activo, abrir el proyecto ya deja el FA calibrado: no hay que pulsar
    /// nada para que EN PPTO cuadre con COSTO (es el 62,80 que en Excel se tecleaba hasta lograrlo).
    /// </summary>
    [Fact]
    public void FaAutomatico_CuadraElFaAlCargarSinPulsarNada()
    {
        var session = SesionCalibrableBase();
        var vm = CrearVm(session, faAutomatico: true);

        vm.Load(session.ProjectInfo);

        Assert.Equal(79.20m, session.ProjectInfo.IndiceMoFa);
        Assert.Equal(CurrencyFormat.Clp(10_000_000m), vm.MoEnPptoLabel);
        Assert.Equal(vm.MoEnPptoLabel, vm.MoCostoRealLabel);
        Assert.False(vm.MoDeltaEsAviso);
        Assert.StartsWith("FA automático", vm.FaResultado);
        // El catálogo y el itemizado quedan recalibrados con el mismo índice.
        Assert.Equal(99_000m, session.Materiales.First(m => m.Codigo == "PCT-01").MoClp);
        Assert.Equal(99_000m, session.Cubicacion.First(r => r.Codigo == "PCT-01").MoClp);
    }

    /// <summary>
    /// Proyecto nuevo: el FA no nace con valor de fábrica ni con el 62,80 heredado de la planilla.
    /// Arranca en 0 (sin calibrar) y queda guardado así en el proyecto.
    /// </summary>
    [Fact]
    public void FaAutomatico_ProyectoNuevo_ArrancaEnCeroSinCalibrar()
    {
        var session = new BudgetProjectSession();

        var vm = CrearVm(session, faAutomatico: true);
        vm.Load(session.ProjectInfo);

        Assert.Equal(0m, session.ProjectInfo.IndiceMoFa);
        Assert.Equal("0,00", vm.IndiceMoFaText);
    }

    /// <summary>Con el modo apagado el FA vigente se respeta, aunque no cuadre con la obra.</summary>
    [Fact]
    public void FaAutomatico_Desactivado_RespetaElFaVigente()
    {
        var session = SesionCalibrableBase();
        var vm = CrearVm(session, faAutomatico: false);

        vm.Load(session.ProjectInfo);

        Assert.Equal(0m, session.ProjectInfo.IndiceMoFa);
        Assert.True(vm.MoDeltaEsAviso);
    }

    /// <summary>Activar el botón cuadra la fila de inmediato, sin pulsar Calcular FA.</summary>
    [Fact]
    public void FaAutomatico_AlActivarlo_CuadraElFaVigente()
    {
        var session = SesionCalibrableBase();
        var vm = CrearVm(session, faAutomatico: false);
        vm.Load(session.ProjectInfo);
        Assert.Equal(0m, session.ProjectInfo.IndiceMoFa);

        vm.FaAutomatico = true;

        Assert.Equal(79.20m, session.ProjectInfo.IndiceMoFa);
        Assert.False(vm.MoDeltaEsAviso);
    }

    /// <summary>
    /// Escribir un FA a mano manda sobre el automático: el modo se apaga para no pisar el valor que
    /// el estimador acaba de fijar, y la pantalla lo dice explícitamente.
    /// </summary>
    [Fact]
    public void FaAutomatico_EscribirAMano_ApagaElModoYRespetaElValor()
    {
        var session = SesionCalibrableBase();
        var vm = CrearVm(session, faAutomatico: true);
        vm.Load(session.ProjectInfo);

        vm.IndiceMoFaText = N2(50m);

        Assert.False(vm.FaAutomatico);
        Assert.Equal(50m, session.ProjectInfo.IndiceMoFa);
        Assert.Contains("Automático quedó desactivado", vm.FaResultado);
        // 50.000 × 2,5 % × 50 = 62.500
        Assert.Equal(62_500m, session.Materiales.First(m => m.Codigo == "PCT-01").MoClp);
    }

    /// <summary>Sin plan de M.O. el modo automático no puede cuadrar nada y deja el índice intacto.</summary>
    [Fact]
    public void FaAutomatico_SinPlanDeManoDeObra_NoTocaElIndice()
    {
        var session = new BudgetProjectSession();
        var vm = CrearVm(session, faAutomatico: true);

        vm.Load(session.ProjectInfo);

        Assert.Equal(MaterialMoResolver.IndiceMoFaDefault, session.ProjectInfo.IndiceMoFa);
        Assert.True(vm.FaAvisoVisible);
    }
}
