using System;
using System.Collections.Generic;

namespace QubiCostElectrical.Tests.Fixtures;

internal static class ProjectFileDataFixtures
{
    public static ProjectFileData BuildFullProject()
    {
        return new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto completo test",
                Cliente = "Cliente test",
                Ubicacion = "Santiago",
                UsuarioNombre = "Test User",
                UsuarioRol = "Ingeniero",
                TipoProyecto = "Edificio comercial",
                MonedaPresupuesto = "UF",
                EstadoPresupuesto = "Revisado",
                FechaCreacion = new DateTime(2026, 1, 15),
                FechaModificacion = new DateTime(2026, 6, 18),
                ValorUfClp = 38000m,
                TipoCambioUsdClp = 950m,
                FechaTipoCambio = new DateTime(2026, 6, 18),
                AutoActualizarIndicadores = true,
                GastosGeneralesPct = 12.5m,
                UtilidadPct = 15.0m,
                HerramientasPct = 1.5m,
                MermasPct = 2.0m,
                ApuCostingMode = "CubicacionManda",
                RiesgoPct = 3.0m,
                DescuentoPct = 0m,
                IvaPct = 19m,
                IncluirIvaEnTotal = true,
                MetrosCuadrados = 2500m,
                PriceExpiryWarningDays = 90,
                PriceExpiryCriticalDays = 120,
                Variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    ["factor_altura"] = 1.15m,
                    ["factor_dificultad"] = 1.05m
                },

                HistorialEstados = new List<BudgetStatusHistoryRow>
                {
                    new() { Id = "H1", EstadoAnterior = "Borrador", EstadoNuevo = "Revisado" }
                },
                CubicacionCellComments = new List<CubicacionCellCommentRow>
                {
                    new() { Id = "CC1", ItemId = "P1", Comment = "Verificar precio" }
                },
                TechnicalValidationRules = new List<TechnicalValidationRuleRow>
                {
                    new() { Id = "TV1", Nombre = "Cantidad > 0" }
                },
                RealCosts = new List<RealCostRow>
                {
                    new() { Id = "RC1", ItemId = "P1", CostoRealUf = 100m }
                },
                PartidaWorkflow = new List<PartidaWorkflowRow>
                {
                    new() { ItemId = "P1", Estado = "Completo" }
                },
                PartidaTimeline = new List<PartidaChangeTimelineRow>
                {
                    new() { Id = "PT1", ItemId = "P1", Campo = "Cantidad", ValorAnterior = "10", ValorNuevo = "20" }
                },
                ProactiveAlertRules = new List<ProactiveAlertRuleRow>
                {
                    new() { Id = "AR1", Nombre = "Alerta costo", Tipo = "CostoTotalUf", Umbral = 10m }
                },
                AntecedentesTecnicos = new List<AntecedenteTecnicoRow>
                {
                    new()
                    {
                        Id = "AT1",
                        Tipo = AntecedenteTecnicoTipo.Plano,
                        Codigo = "EL-001",
                        Revision = "A",
                        Descripcion = "Plano eléctrico general",
                        EsVigente = true,
                        IncluidoEnAlcance = true
                    }
                },
                LaborDaySettings = new LaborDaySettingsRow
                {
                    HorasPorJornada = 8m,
                    DiasPorSemana = 5m,
                    FactorProductividad = 1m,
                    ValorJornadaUf = 0.5m,
                    CuadrillaDefault = "1 maestro + 1 ayudante"
                },
                OfertaPdf = new OfferPdfSettings()
            },
            Itemizado = new List<ItemizadoRow>
            {
                new() { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "ML", Cantidad = 100m },
                new() { Id = "P2", Item = "1.2", Descripcion = "Tablero", Unidad = "UN", Cantidad = 3m }
            },
            Materiales = new List<MaterialRow>
            {
                new() { Codigo = "MAT001", Descripcion = "Cable 2.5mm²", Unidad = "ML", Categoria = "Cables", MatUf = 0.35m }
            },
            Cubicacion = new List<CubicacionRow>
            {
                new() { Id = "C1", ItemId = "P1", Tipo = "MAT", Codigo = "MAT001", Descripcion = "Cable 2.5mm²", Unidad = "ML", Cantidad = 100m, MatUf = 0.35m }
            },
            Plantillas = new List<TemplateRow>
            {
                new() { Nombre = "Plantilla base" }
            },
            ConjuntosElectricos = new List<ConjuntoElectricoRow>
            {
                new()
                {
                    Id = "CE1",
                    Codigo = "PE-01",
                    Nombre = "Punto enchufe estándar",
                    TipoConjunto = ConjuntoTipo.PuntoEnchufe,
                    Recursos = new List<ConjuntoRecursoRow>
                    {
                        new() { Id = "CR1", Codigo = "MAT001", Descripcion = "Cable 2.5mm²", Unidad = "ML", Rendimiento = 3m }
                    }
                }
            },
            Versiones = new List<BudgetVersionRow>
            {
                new() { Id = "V1", Codigo = "V1", Nombre = "Preliminar" }
            },
            Apus = new List<ApuRow>
            {
                new()
                {
                    Id = "A1",
                    ItemId = "P1",
                    Descripcion = "APU Canalización",
                    CondicionesLaborales = new ApuCondicionesLaboralesRow
                    {
                        FactorAlturaPct = 15m,
                        EdificioEnOperacion = true,
                        RecargoOperacionPct = 20m,
                        NivelDificultad = ApuNivelDificultad.Alta,
                        Ubicacion = ApuUbicacionLabor.Altura
                    }
                }
            },
            OrdenesCambio = new List<ChangeOrderRow>
            {
                new() { Id = "OC1", Titulo = "Cambio tablero", DeltaVentaUf = 15m }
            },
            Zonas = new List<ZonaProyecto>
            {
                new() { Id = "Z1", Nombre = "Piso 1", Orden = 1, Activa = true }
            },
            Mediciones = new List<MeasurementLineRow>
            {
                new() { Id = "M1", ItemId = "P1", Comentario = "Medición piso 1", Unidades = 100m }
            },

            CubicacionSC = new List<CuadroCargaSCRow>
            {
                new() { Id = "SC1", Nombre = "TDA SC", CircuitosProyectados = 40 }
            },
            CubicacionScConfig = CubicacionScConfig.Default(),
            QtyHeadersByItem = new Dictionary<string, string[]>
            {
                ["P1"] = new[] { "Piso 1", "Piso 2" }
            },
            ActiveVersionId = "V1"
        };
    }

    public static ProjectFileData BuildFullProjectModified()
    {
        var data = BuildFullProject();
        data.Proyecto.Nombre = "Proyecto modificado incremental";
        data.Proyecto.Cliente = "Cliente modificado";
        data.Proyecto.MetrosCuadrados = 3200m;
        data.Proyecto.Variables["factor_altura"] = 1.25m;

        data.Itemizado.Add(new ItemizadoRow
        {
            Id = "P3",
            Item = "1.3",
            Descripcion = "Iluminación LED",
            Unidad = "UN",
            Cantidad = 25m
        });

        data.Materiales.Add(new MaterialRow
        {
            Codigo = "MAT002",
            Descripcion = "Luminaria LED",
            Unidad = "UN",
            Categoria = "Iluminación",
            MatUf = 1.20m
        });

        data.Cubicacion.Add(new CubicacionRow
        {
            Id = "C2",
            ItemId = "P3",
            Tipo = "MAT",
            Codigo = "MAT002",
            Descripcion = "Luminaria LED",
            Unidad = "UN",
            Cantidad = 25m,
            MatUf = 1.20m
        });

        data.Apus[0].Descripcion = "APU Canalización revisado";
        data.Apus[0].CondicionesLaborales.FactorAlturaPct = 18m;

        data.Plantillas[0].Nombre = "Plantilla revisada";
        data.ConjuntosElectricos[0].Nombre = "Conjunto revisado";
        data.Versiones[0].Nombre = "Revisión cliente";
        data.OrdenesCambio[0].DeltaVentaUf = 22m;
        data.Zonas.Add(new ZonaProyecto { Id = "Z2", Nombre = "Piso 2", Orden = 2, Activa = true });
        data.Mediciones[0].Unidades = 120m;
        data.CubicacionSC[0].CircuitosProyectados = 52;
        data.QtyHeadersByItem["P1"] = new[] { "Piso 1", "Piso 2", "Piso 3" };
        data.ActiveVersionId = "V1";

        return data;
    }
}
