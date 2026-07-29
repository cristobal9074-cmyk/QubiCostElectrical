using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Fixtures;

internal static class ProjectFileDataAssertions
{
    public static void AssertEquivalent(ProjectFileData expected, ProjectFileData actual)
    {
        Assert.NotNull(expected.Proyecto);
        Assert.NotNull(actual.Proyecto);

        var pExp = expected.Proyecto;
        var pAct = actual.Proyecto;

        Assert.Equal(pExp.Nombre, pAct.Nombre);
        Assert.Equal(pExp.Cliente, pAct.Cliente);
        Assert.Equal(pExp.Ubicacion, pAct.Ubicacion);
        Assert.Equal(pExp.UsuarioNombre, pAct.UsuarioNombre);
        Assert.Equal(pExp.UsuarioRol, pAct.UsuarioRol);
        Assert.Equal(pExp.TipoProyecto, pAct.TipoProyecto);
        Assert.Equal(pExp.MonedaPresupuesto, pAct.MonedaPresupuesto);
        Assert.Equal(pExp.EstadoPresupuesto, pAct.EstadoPresupuesto);
        Assert.Equal(pExp.FechaCreacion, pAct.FechaCreacion);
        Assert.Equal(pExp.FechaModificacion, pAct.FechaModificacion);
        Assert.Equal(pExp.ValorUfClp, pAct.ValorUfClp);
        Assert.Equal(pExp.TipoCambioUsdClp, pAct.TipoCambioUsdClp);
        Assert.Equal(pExp.FechaTipoCambio, pAct.FechaTipoCambio);
        Assert.Equal(pExp.AutoActualizarIndicadores, pAct.AutoActualizarIndicadores);
        Assert.Equal(pExp.GastosGeneralesPct, pAct.GastosGeneralesPct);
        Assert.Equal(pExp.UtilidadPct, pAct.UtilidadPct);
        Assert.Equal(pExp.HerramientasPct, pAct.HerramientasPct);
        Assert.Equal(pExp.MermasPct, pAct.MermasPct);
        Assert.Equal(pExp.ApuCostingMode, pAct.ApuCostingMode);
        Assert.Equal(pExp.RiesgoPct, pAct.RiesgoPct);
        Assert.Equal(pExp.DescuentoPct, pAct.DescuentoPct);
        Assert.Equal(pExp.IvaPct, pAct.IvaPct);
        Assert.Equal(pExp.IncluirIvaEnTotal, pAct.IncluirIvaEnTotal);
        Assert.Equal(pExp.MetrosCuadrados, pAct.MetrosCuadrados);
        Assert.Equal(pExp.PriceExpiryWarningDays, pAct.PriceExpiryWarningDays);
        Assert.Equal(pExp.PriceExpiryCriticalDays, pAct.PriceExpiryCriticalDays);

        Assert.Equal(pExp.Variables.Count, pAct.Variables.Count);
        foreach (var kv in pExp.Variables)
            Assert.Equal(kv.Value, pAct.Variables[kv.Key]);



        Assert.Equal(pExp.HistorialEstados.Count, pAct.HistorialEstados.Count);
        if (pExp.HistorialEstados.Count > 0)
        {
            Assert.Equal(pExp.HistorialEstados[0].EstadoAnterior, pAct.HistorialEstados[0].EstadoAnterior);
            Assert.Equal(pExp.HistorialEstados[0].EstadoNuevo, pAct.HistorialEstados[0].EstadoNuevo);
        }

        Assert.Equal(pExp.CubicacionCellComments.Count, pAct.CubicacionCellComments.Count);
        if (pExp.CubicacionCellComments.Count > 0)
            Assert.Equal(pExp.CubicacionCellComments[0].Comment, pAct.CubicacionCellComments[0].Comment);

        Assert.Equal(pExp.TechnicalValidationRules.Count, pAct.TechnicalValidationRules.Count);
        if (pExp.TechnicalValidationRules.Count > 0)
            Assert.Equal(pExp.TechnicalValidationRules[0].Nombre, pAct.TechnicalValidationRules[0].Nombre);

        Assert.Equal(pExp.RealCosts.Count, pAct.RealCosts.Count);
        if (pExp.RealCosts.Count > 0)
            Assert.Equal(pExp.RealCosts[0].CostoRealUf, pAct.RealCosts[0].CostoRealUf);

        Assert.Equal(pExp.PartidaWorkflow.Count, pAct.PartidaWorkflow.Count);
        if (pExp.PartidaWorkflow.Count > 0)
            Assert.Equal(pExp.PartidaWorkflow[0].Estado, pAct.PartidaWorkflow[0].Estado);

        Assert.Equal(pExp.PartidaTimeline.Count, pAct.PartidaTimeline.Count);
        if (pExp.PartidaTimeline.Count > 0)
            Assert.Equal(pExp.PartidaTimeline[0].ValorNuevo, pAct.PartidaTimeline[0].ValorNuevo);

        Assert.Equal(pExp.ProactiveAlertRules.Count, pAct.ProactiveAlertRules.Count);
        if (pExp.ProactiveAlertRules.Count > 0)
            Assert.Equal(pExp.ProactiveAlertRules[0].Umbral, pAct.ProactiveAlertRules[0].Umbral);

        Assert.Equal(pExp.AntecedentesTecnicos.Count, pAct.AntecedentesTecnicos.Count);
        if (pExp.AntecedentesTecnicos.Count > 0)
        {
            Assert.Equal(pExp.AntecedentesTecnicos[0].Codigo, pAct.AntecedentesTecnicos[0].Codigo);
            Assert.Equal(pExp.AntecedentesTecnicos[0].Revision, pAct.AntecedentesTecnicos[0].Revision);
        }

        Assert.NotNull(pExp.LaborDaySettings);
        Assert.NotNull(pAct.LaborDaySettings);
        Assert.Equal(pExp.LaborDaySettings.HorasPorJornada, pAct.LaborDaySettings.HorasPorJornada);
        Assert.Equal(pExp.LaborDaySettings.ValorJornadaUf, pAct.LaborDaySettings.ValorJornadaUf);

        Assert.Equal(expected.Itemizado.Count, actual.Itemizado.Count);
        for (int i = 0; i < expected.Itemizado.Count; i++)
        {
            Assert.Equal(expected.Itemizado[i].Id, actual.Itemizado[i].Id);
            Assert.Equal(expected.Itemizado[i].Item, actual.Itemizado[i].Item);
            Assert.Equal(expected.Itemizado[i].Descripcion, actual.Itemizado[i].Descripcion);
            Assert.Equal(expected.Itemizado[i].Unidad, actual.Itemizado[i].Unidad);
            Assert.Equal(expected.Itemizado[i].Cantidad, actual.Itemizado[i].Cantidad);
        }

        Assert.Equal(expected.Materiales.Count, actual.Materiales.Count);
        for (int i = 0; i < expected.Materiales.Count; i++)
        {
            Assert.Equal(expected.Materiales[i].Codigo, actual.Materiales[i].Codigo);
            Assert.Equal(expected.Materiales[i].Descripcion, actual.Materiales[i].Descripcion);
            Assert.Equal(expected.Materiales[i].MatUf, actual.Materiales[i].MatUf);
        }

        Assert.Equal(expected.Cubicacion.Count, actual.Cubicacion.Count);
        for (int i = 0; i < expected.Cubicacion.Count; i++)
        {
            Assert.Equal(expected.Cubicacion[i].Id, actual.Cubicacion[i].Id);
            Assert.Equal(expected.Cubicacion[i].ItemId, actual.Cubicacion[i].ItemId);
            Assert.Equal(expected.Cubicacion[i].Codigo, actual.Cubicacion[i].Codigo);
            Assert.Equal(expected.Cubicacion[i].Cantidad, actual.Cubicacion[i].Cantidad);
            Assert.Equal(expected.Cubicacion[i].MatUf, actual.Cubicacion[i].MatUf);
        }

        Assert.Equal(expected.Plantillas.Count, actual.Plantillas.Count);
        if (expected.Plantillas.Count > 0)
            Assert.Equal(expected.Plantillas[0].Nombre, actual.Plantillas[0].Nombre);

        Assert.Equal(expected.ConjuntosElectricos.Count, actual.ConjuntosElectricos.Count);
        if (expected.ConjuntosElectricos.Count > 0)
        {
            Assert.Equal(expected.ConjuntosElectricos[0].Codigo, actual.ConjuntosElectricos[0].Codigo);
            Assert.Equal(expected.ConjuntosElectricos[0].Nombre, actual.ConjuntosElectricos[0].Nombre);
            Assert.Equal(expected.ConjuntosElectricos[0].Recursos.Count, actual.ConjuntosElectricos[0].Recursos.Count);
        }

        Assert.Equal(expected.Versiones.Count, actual.Versiones.Count);
        if (expected.Versiones.Count > 0)
            Assert.Equal(expected.Versiones[0].Nombre, actual.Versiones[0].Nombre);

        Assert.Equal(expected.Apus.Count, actual.Apus.Count);
        if (expected.Apus.Count > 0)
        {
            Assert.Equal(expected.Apus[0].ItemId, actual.Apus[0].ItemId);
            Assert.Equal(expected.Apus[0].Descripcion, actual.Apus[0].Descripcion);
            Assert.NotNull(actual.Apus[0].CondicionesLaborales);
            Assert.Equal(expected.Apus[0].CondicionesLaborales.FactorAlturaPct, actual.Apus[0].CondicionesLaborales.FactorAlturaPct);
            Assert.Equal(expected.Apus[0].CondicionesLaborales.EdificioEnOperacion, actual.Apus[0].CondicionesLaborales.EdificioEnOperacion);
        }

        Assert.Equal(expected.OrdenesCambio.Count, actual.OrdenesCambio.Count);
        if (expected.OrdenesCambio.Count > 0)
            Assert.Equal(expected.OrdenesCambio[0].DeltaVentaUf, actual.OrdenesCambio[0].DeltaVentaUf);

        Assert.Equal(expected.Zonas.Count, actual.Zonas.Count);
        for (int i = 0; i < expected.Zonas.Count; i++)
        {
            Assert.Equal(expected.Zonas[i].Id, actual.Zonas[i].Id);
            Assert.Equal(expected.Zonas[i].Nombre, actual.Zonas[i].Nombre);
        }

        Assert.Equal(expected.Mediciones.Count, actual.Mediciones.Count);
        if (expected.Mediciones.Count > 0)
            Assert.Equal(expected.Mediciones[0].Unidades, actual.Mediciones[0].Unidades);



        Assert.Equal(expected.CubicacionSC.Count, actual.CubicacionSC.Count);
        if (expected.CubicacionSC.Count > 0)
            Assert.Equal(expected.CubicacionSC[0].CircuitosProyectados, actual.CubicacionSC[0].CircuitosProyectados);

        Assert.Equal(expected.QtyHeadersByItem.Count, actual.QtyHeadersByItem.Count);
        foreach (var kv in expected.QtyHeadersByItem)
            Assert.Equal(kv.Value, actual.QtyHeadersByItem[kv.Key]);

        Assert.Equal(expected.ActiveVersionId, actual.ActiveVersionId);
    }
}
