using System.Collections.Generic;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Proyecto determinista para golden-file Excel (sin fechas ni versiones variables).
/// </summary>
internal static class ExcelExportGoldenFixture
{
    internal static readonly DateTime FixedExportDate = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    internal static readonly DateTime FixedOfferDate = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Unspecified);

    internal const string ProjectName = "Proyecto Golden Export";
    internal const string ClientName = "Cliente Golden SA";

    internal static readonly string[] ExpectedSheetNames =
    [
        "00_CONTROL",
        "01_DATOS_PROYECTO",
        "02_ITEMIZADO_CLIENTE",
        "03_CUBICACION_DETALLE",
        "04_APU",
        "05_LISTA_PRECIOS",
        "06_HOJA_COSTO",
        "08_REVISION_FINAL",
        "09_OFERTA_CLIENTE",
        "10_AUDITORIA_PRECIOS",
        "11_FORMATO_COMPRA"
    ];

    internal static ExportProjectContext Create()
    {
        var data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = ProjectName,
                Cliente = ClientName,
                GastosGeneralesPct = 14m,
                UtilidadPct = 18m,
                ValorUfClp = 37000m,
                EstadoPresupuesto = "Borrador",
                OfertaPdf = new OfferPdfSettings
                {
                    OfferDate = FixedOfferDate,
                    OfferNumber = "OF-2026-001",
                    RevisionTag = "Rev A",
                    CompanyName = "Qubi Instalaciones SpA"
                }
            },
            Itemizado =
            [
                new ItemizadoRow { Id = "TIT001", Item = "1", Descripcion = "INSTALACIONES ELÉCTRICAS", Unidad = "", Cantidad = 0m, EsTitulo = true },
                new ItemizadoRow { Id = "P001", Item = "1.1", Descripcion = "Canalización PVC 20mm", Unidad = "ML", Cantidad = 50m }
            ],
            Cubicacion =
            [
                new CubicacionRow { Id = "C001", ItemId = "P001", Tipo = "MAT", Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Cantidad = 50m, MatUf = 0.12m, MoUf = 0m },
                new CubicacionRow { Id = "C002", ItemId = "P001", Tipo = "MO", Codigo = "MO001", Descripcion = "Instalación PVC", Unidad = "ML", Cantidad = 50m, MatUf = 0m, MoUf = 0.08m }
            ],
            Materiales =
            [
                new MaterialRow { Codigo = "MAT001", Descripcion = "Tubo PVC 20mm", Unidad = "ML", Categoria = "Tubería", MatUf = 0.12m },
                new MaterialRow { Codigo = "MO001", Descripcion = "Instalación PVC", Unidad = "ML", Categoria = "MO", MoUf = 0.08m }
            ]
        };

        var budget = new BudgetCalculationService().CalculateBudget(
            data.Cubicacion,
            BudgetSettings.FromProject(data.Proyecto),
            data.Apus,
            data.Itemizado,
            data.OrdenesCambio);

        return new ExportProjectContext
        {
            Data = data,
            ExportedBy = "GoldenTest",
            ExportedAt = FixedExportDate,
            OfferNetAmountUf = budget.TotalVentaNetoUf
        };
    }
}
