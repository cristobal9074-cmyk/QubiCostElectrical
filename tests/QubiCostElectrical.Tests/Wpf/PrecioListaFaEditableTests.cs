using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// El FA del alta rápida de materiales es editable (no un rótulo fijo) y se deriva del plan de
/// mano de obra cuando el usuario lo pide. Al aplicarlo, la M.O. del catálogo y del itemizado se
/// recalibran igual que el resto de la app.
/// </summary>
public sealed class PrecioListaFaEditableTests
{
    private sealed class FakeBank : IPriceBankStore
    {
        private readonly List<PriceBankEntry> entries;

        public FakeBank(params PriceBankEntry[] entries) => this.entries = entries.ToList();

        public IReadOnlyList<PriceBankEntry> GetAll() => entries.ToList();
        public PriceBankEntry? GetByCodigo(string codigo) => entries.FirstOrDefault(e => e.Codigo == codigo);
        public IReadOnlyList<PriceBankEntry> Search(string text) => GetAll();
        public void Upsert(PriceBankEntry entry) { }
        public bool Delete(string codigo) => false;
        public int DeleteAll() => 0;
        public int UpsertMany(IEnumerable<PriceBankEntry> nuevos) => 0;
        public int Count() => entries.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> seed) => 0;
    }

    private static (BudgetProjectSession Session, PrecioListaResumenVm Vm) Crear()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.MoTotalClp = 10_000_000m;
        session.Materiales.Add(new MaterialRow
        {
            Codigo = "TAB-001",
            Descripcion = "Tablero",
            MatClp = 50_000m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });
        session.Cubicacion.Add(new CubicacionRow { Codigo = "TAB-001", Cantidad = 10m });

        var banco = new FakeBank(new PriceBankEntry
        {
            Codigo = "TAB-001",
            Descripcion = "Tablero",
            MatClp = 50_000m,
            MoClp = 0m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 2.5m
        });

        return (session, new PrecioListaResumenVm(session, banco));
    }

    private static decimal FaDerivado(ProjectFileData? data, ProjectInfoRow proyecto) =>
        FactorAjusteMoService.CalcularParaProyecto(data, proyecto).Fa ?? 0m;

    [Fact]
    public void IndiceMoFaTexto_ArrancaEnCeroSinCalibrar()
    {
        var (_, vm) = Crear();

        Assert.Equal(0m, vm.IndiceMoFa);
        Assert.StartsWith("0", vm.IndiceMoFaTexto);
    }

    [Fact]
    public void IndiceMoFaTexto_Editado_PersisteEnElProyectoYRecalibraLaLista()
    {
        var (session, vm) = Crear();

        vm.IndiceMoFaTexto = 80m.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

        Assert.Equal(80m, session.ProjectInfo.IndiceMoFa);
        // 50.000 × 2,5 % × 80 = 100.000
        Assert.Equal(100_000m, session.Materiales.Single(m => m.Codigo == "TAB-001").MoClp);
        Assert.Equal(100_000m, vm.Items.Cast<PrecioListaMaterialVm>().Single().MoClp);
    }

    [Fact]
    public void IndiceMoFaTexto_Invalido_NoCambiaElIndice()
    {
        var (session, vm) = Crear();

        vm.IndiceMoFaTexto = "no-es-numero";

        Assert.Equal(0m, session.ProjectInfo.IndiceMoFa);
    }

    /// <summary>Escribir 0 vuelve al estado "sin calibrar": la M.O. por % se recalcula con factor 1.</summary>
    [Fact]
    public void IndiceMoFaTexto_Cero_DejaLaMoPorcentajeConFactorNeutro()
    {
        var (session, vm) = Crear();
        vm.IndiceMoFaTexto = 80m.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

        vm.IndiceMoFaTexto = 0m.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

        Assert.Equal(0m, session.ProjectInfo.IndiceMoFa);
        // 50.000 × 2,5 % × 1 = 1.250
        Assert.Equal(1_250m, session.Materiales.Single(m => m.Codigo == "TAB-001").MoClp);
    }

    [Fact]
    public void CalcularFaCommand_DerivaElIndiceDesdeElPlanDeManoDeObra()
    {
        var (session, vm) = Crear();
        decimal esperado = FaDerivado(session.Data, session.ProjectInfo);

        vm.CalcularFaCommand.Execute(null);

        Assert.True(esperado > 0m);
        Assert.Equal(esperado, session.ProjectInfo.IndiceMoFa);
        Assert.Equal(esperado, vm.IndiceMoFa);
    }

    [Fact]
    public void CalcularFaCommand_SinPlanDeManoDeObra_NoDerivaYExplicaPorQue()
    {
        var session = new BudgetProjectSession();
        var vm = new PrecioListaResumenVm(session, new FakeBank());
        decimal antes = session.ProjectInfo.IndiceMoFa;

        vm.CalcularFaCommand.Execute(null);

        Assert.Equal(antes, session.ProjectInfo.IndiceMoFa);
        Assert.True(vm.FaSinCalibrar);
        Assert.Contains("plan de mano de obra", vm.ExplicacionFa);
        Assert.True(vm.FaDerivadoVisible);
        Assert.Contains("Sin plan de mano de obra", vm.FaDerivadoTexto);
    }

    /// <summary>
    /// Sin plan de mano de obra el FA queda en 0 y hay que decir por qué: la explicación nombra la
    /// causa (dotación/sueldos sin cargar) y el efecto (la M.O. se calcula con factor neutro), no
    /// solo el valor.
    /// </summary>
    [Fact]
    public void ExplicacionFa_SinPlanDeManoDeObra_DiceQueFaltaCargarManoDeObra()
    {
        var session = new BudgetProjectSession();
        var vm = new PrecioListaResumenVm(session, new FakeBank());

        Assert.True(vm.FaSinCalibrar);
        Assert.Contains("plan de mano de obra", vm.ExplicacionFa);
        Assert.Contains("factor neutro", vm.ExplicacionFa);
        Assert.Contains("Mano de obra", vm.ExplicacionFa);
    }

    /// <summary>Con proyecto y plan listo, el FA sigue en 0 hasta que alguien lo calibra: se explica igual.</summary>
    [Fact]
    public void ExplicacionFa_ConPlanListoPeroSinCalibrar_InvitaACalcularFa()
    {
        var (_, vm) = Crear();

        Assert.True(vm.FaSinCalibrar);
        Assert.Contains("Calcular FA", vm.ExplicacionFa);
    }

    [Fact]
    public void ExplicacionFa_ConFaCalibrado_ExplicaQueLaMoSeCalculaSola()
    {
        var (_, vm) = Crear();
        vm.IndiceMoFaTexto = 80m.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

        Assert.False(vm.FaSinCalibrar);
        Assert.Contains("calibrado", vm.ExplicacionFa);
        Assert.Contains("Material × % × FA", vm.ExplicacionFa);
    }

    /// <summary>La explicación del FA es dato de UI: tiene que avisar cuando cambia el índice.</summary>
    [Fact]
    public void ExplicacionFa_AvisaCuandoCambiaElIndice()
    {
        var (_, vm) = Crear();
        var avisos = new List<string?>();
        vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        vm.IndiceMoFaTexto = 80m.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

        Assert.Contains(nameof(PrecioListaResumenVm.ExplicacionFa), avisos);
        Assert.Contains(nameof(PrecioListaResumenVm.FaSinCalibrar), avisos);
    }

    [Fact]
    public void FaDerivadoTexto_ComparaElIndiceVigenteConElDelPlan()
    {
        var (_, vm) = Crear();

        Assert.True(vm.FaDerivadoVisible);
        Assert.Contains("Calcular FA", vm.FaDerivadoTexto);
    }
}
