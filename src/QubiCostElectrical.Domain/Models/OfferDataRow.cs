namespace QubiCostElectrical;

public sealed class OfferPlanReferenceRow
{
    public string Revision { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Disciplina { get; set; } = "Electricidad";
}

public sealed class OfferProductSpecificationRow
{
    public string Categoria { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Especificacion { get; set; } = "";
}

public sealed class OfferPdfSettings
{
    public string CompanyName { get; set; } = "";
    public string CompanyRut { get; set; } = "";
    public string CompanyAddress { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyEmail { get; set; } = "";
    public string CompanyWebsite { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public string ClientLogoPath { get; set; } = "";

    public string OfferNumber { get; set; } = "";
    public string RevisionTag { get; set; } = "Rev 0";
    public string OfferTitle { get; set; } = "PRESUPUESTO INSTALACIONES ELÉCTRICAS";
    public DateTime OfferDate { get; set; } = DateTime.Today;
    public string RecipientName { get; set; } = "Sr.";
    public string RecipientCompany { get; set; } = "";
    public string Reference { get; set; } = "Presupuesto Instalaciones Eléctricas";
    public string MontajeText { get; set; } = "Instalaciones Eléctricas y Canalización de corrientes débiles.";
    public string ProjectDuration { get; set; } = "18 meses";
    public int ValidityDays { get; set; } = 30;
    public bool IncludeIvaNote { get; set; } = true;
    public bool UseManualNetAmountUf { get; set; }
    public decimal ManualNetAmountUf { get; set; }
    public bool ShowUnitPricesInItemizado { get; set; } = true;
    public bool ShowCostComponentDistribution { get; set; } = true;
    /// <summary>
    /// Si true, el resumen de la oferta comercial (PDF y Excel) muestra los precios enteros de venta
    /// en las partidas y oculta el desglose de Gastos Generales y Utilidad al final.
    /// Si false, muestra los precios de costo directo en las partidas y desglosa GG y UT en el totalizador.
    /// </summary>
    public bool OcultarGgYUtilidadEnResumen { get; set; } = true;

    public string ScopeText { get; set; } = "Suministro y montaje de tableros eléctricos y sus equipamientos.\r\nSuministro y montaje de alimentadores generales y de distribución.\r\nSuministro y montaje de escalerillas y bandejas porta conductores según proyecto.\r\nMontaje de equipos de iluminación.\r\nCanalización y cableado de distribución de iluminación, enchufes, computación, fuerza y climatización.\r\nCanalización y enlauchado de sistemas de corrientes débiles según proyecto.";
    public string ExclusionsText { get; set; } = "No se incluyen equipos de la compañía eléctrica, celda de medida ni pagos por ningún concepto a la distribuidora de la zona.\r\nNo se incluye el suministro de equipos de iluminación.\r\nNo se incluye suministro ni montaje de tableros de otras especialidades, salvo los indicados en proyecto.\r\nNo se incluye el sistema de cableado estructurado, CCTV, detección de incendio, citofonía, control de acceso ni otros sistemas de corrientes débiles.\r\nNo se incluyen izamientos ni desplazamientos de materiales y equipos (verticales y horizontales), los cuales deben ser considerados por la constructora.\r\nNo se consideran obras civiles: cámaras, excavaciones, retapes, compactados, bases de montaje, calados, pinturas, etc.\r\nNo se consideran los circuitos no proyectados en cuadro de carga ni en diagramas unilineales.";
    public string CommercialConditionsText { get; set; } = "Monto : De acuerdo con el valor neto indicado en la oferta comercial.\r\nImpuestos : Se debe agregar 19% IVA.\r\nMoneda del contrato : El contrato será firmado en Unidades de Fomento (UF).\r\nAnticipo : 15% contado.\r\nSaldo : Estados de pago mensuales conforme a avance de obra (pago a 30 días de recepción de factura).\r\nSuministro de equipos : Facturados y cobrados al 100% del suministro, según programa oficial de obra.\r\nPlazo : Según duración indicada en la oferta.\r\nPóliza de garantía de ejecución inmediata : 15% por anticipo y 5% por fiel cumplimiento.\r\nRetenciones : 5% de cada estado de pago hasta enterar un 5% del monto total de la oferta.\r\nDevolución de retenciones : Canjeable por pólizas de garantía a la recepción provisoria de la obra.\r\nMultas : 0,1% (1 por mil) por cada día de atraso imputable al contratista.\r\nTope de multas : 5% del monto del contrato.";
    public string NormativeAssumptionsText { get; set; } = "Cumplimiento estricto de la normativa eléctrica vigente en Chile según los Pliegos Técnicos Normativos RIC (N° 01 al N° 19) de la SEC y DS N° 8/2019.\r\nTodos los materiales, conductores, canalizaciones y aparamenta de protección consideran certificación y sello SEC.\r\nSe considera la tramitación y obtención de la declaración de puesta en servicio TE1 ante la SEC para la recepción final.";
    public string ObservationsText { get; set; } = "Se consultan las siguientes marcas de equipos o similar calidad, previa aprobación por parte de la ITO.";
    public bool IncludeEquipmentBrands { get; set; } = true;
    public string EquipmentBrandsText { get; set; } = "Tableros Eléctricos : BETEL, EMBU o equivalente técnico.\r\nEquipamiento de tableros : Schneider Electric, Legrand o equivalente técnico.\r\nGeneradores Eléctricos : ELECTROPOWER o equivalente técnico.\r\nArtefactos : Living Light o equivalente técnico.";
    public bool IncludeDrawingList { get; set; } = true;

    public bool IncludePresentationLetter { get; set; } = true;
    public string PresentationLetterTitle { get; set; } = "CARTA DE PRESENTACIÓN";
    public string PresentationLetterText { get; set; } = "Mediante la presente, adjuntamos nuestra oferta por las obras eléctricas y canalizaciones de corrientes débiles, para la ejecución de las instalaciones del proyecto indicado. El alcance, exclusiones, condiciones comerciales, marcas y listado de antecedentes forman parte integral de esta oferta.\r\n\r\nEn caso de cualquier consulta, por favor sírvase dirigirla a nuestros contactos indicados al pie de esta carta.";

    public string SignatureName { get; set; } = "";
    public string SignatureRut { get; set; } = "";
    public string SignatureRole { get; set; } = "";
    public string SignatureRegistroSec { get; set; } = "";
    public string SignatureSecClass { get; set; } = "Clase A";
    public string SignaturePhone { get; set; } = "";
    public string SignatureEmail { get; set; } = "";
    public string SignatureImagePath { get; set; } = "";

    /// <summary>Ancho de la imagen de firma en el PDF, en milímetros (0 = predeterminado 46).</summary>
    public int SignatureWidthMm { get; set; } = 0;
    /// <summary>Alto de la imagen de firma en el PDF, en milímetros (0 = predeterminado 16).</summary>
    public int SignatureHeightMm { get; set; } = 0;

    public List<OfferPlanReferenceRow> Planos { get; set; } = new();
    public List<OfferProductSpecificationRow> Productos { get; set; } = new();
}

/// <summary>
/// Arma las secciones de antecedentes de la carta de oferta a partir de los planos
/// escritos en la oferta o de los que se subieron en Antecedentes.
/// Cada disciplina (electricidad, corrientes débiles, telecomunicaciones) es opcional:
/// si no hay planos, esa sección no se imprime.
/// </summary>
public static class OfferLetterPlanCatalog
{
    public static List<OfferPlanReferenceRow> Resolve(OfferPdfSettings? offer, IEnumerable<AntecedenteTecnicoRow>? uploaded)
    {
        var manual = offer?.Planos ?? new List<OfferPlanReferenceRow>();
        if (manual.Count > 0)
            return manual;
        return FromUploads(uploaded);
    }

    public static List<OfferPlanReferenceRow> FromUploads(IEnumerable<AntecedenteTecnicoRow>? rows)
    {
        var list = new List<OfferPlanReferenceRow>();
        foreach (var row in rows ?? Enumerable.Empty<AntecedenteTecnicoRow>())
        {
            if (row == null || !row.EsVigente) continue;
            if (!IsDrawing(row.Tipo)) continue;
            var described = Describe(row);
            if (string.IsNullOrWhiteSpace(described)) continue;
            list.Add(new OfferPlanReferenceRow
            {
                Codigo = (row.Codigo ?? "").Trim(),
                Revision = (row.Revision ?? "").Trim(),
                Descripcion = described,
                Disciplina = string.IsNullOrWhiteSpace(row.Disciplina)
                    ? AntecedenteDisciplina.Electricidad
                    : row.Disciplina.Trim()
            });
        }
        return list;
    }

    public static IReadOnlyList<(string Title, List<OfferPlanReferenceRow> Rows)> Group(IEnumerable<OfferPlanReferenceRow>? planos)
    {
        return (planos ?? Enumerable.Empty<OfferPlanReferenceRow>())
            .GroupBy(p => SectionTitle(p.Disciplina), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Title: g.Key, Rows: g.ToList()))
            .OrderBy(g => Rank(g.Title))
            .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string SectionTitle(string? disciplina)
    {
        var d = (disciplina ?? "").Trim();
        if (d.Length == 0) return "ANTECEDENTES";
        if (Has(d, "lectric") || d.Equals("ELE", StringComparison.OrdinalIgnoreCase))
            return "ANTECEDENTES ELECTRICIDAD";
        if (Has(d, "ebil") || Has(d, "ccdd") || Has(d, "corriente") || d.Equals("CD", StringComparison.OrdinalIgnoreCase))
            return "ANTECEDENTES CORRIENTES DEBILES";
        if (Has(d, "eleco") || Has(d, "telecom") || d.Equals("TEL", StringComparison.OrdinalIgnoreCase))
            return "ANTECEDENTES TELECOMUNICACIONES";
        if (Has(d, "arquit"))
            return "ANTECEDENTES ARQUITECTURA";
        if (Has(d, "espec"))
            return "ANTECEDENTES ESPECIFICACIONES TECNICAS";
        return "ANTECEDENTES " + d.ToUpperInvariant();
    }

    private static bool IsDrawing(string? tipo)
    {
        var t = (tipo ?? "").Trim();
        return t.Length == 0
            || t.Equals(AntecedenteTecnicoTipo.Plano, StringComparison.OrdinalIgnoreCase)
            || t.Equals(AntecedenteTecnicoTipo.Especificacion, StringComparison.OrdinalIgnoreCase);
    }

    private static string Describe(AntecedenteTecnicoRow row)
    {
        var desc = (row.Descripcion ?? "").Trim();
        var code = (row.Codigo ?? "").Trim();
        if (desc.Length == 0 && code.Length == 0)
            return FileStem(row.RutaArchivo);
        if (code.Length == 0 || desc.Contains(code, StringComparison.OrdinalIgnoreCase))
            return desc.Length > 0 ? desc : code;
        return $"{code} {desc}".Trim();
    }

    private static string FileStem(string? path)
    {
        var name = (path ?? "").Trim().TrimEnd('\\', '/');
        var slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
        if (slash >= 0) name = name[(slash + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static bool Has(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static int Rank(string title) => title switch
    {
        "ANTECEDENTES ELECTRICIDAD" => 0,
        "ANTECEDENTES CORRIENTES DEBILES" => 1,
        "ANTECEDENTES TELECOMUNICACIONES" => 2,
        "ANTECEDENTES ESPECIFICACIONES TECNICAS" => 3,
        "ANTECEDENTES ARQUITECTURA" => 4,
        _ => 10
    };
}
