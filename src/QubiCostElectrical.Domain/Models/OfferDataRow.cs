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

    public string OfferNumber { get; set; } = "";
    public string RevisionTag { get; set; } = "Rev 0";
    public string OfferTitle { get; set; } = "PRESUPUESTO INSTALACIONES ELÉCTRICAS";
    public DateTime OfferDate { get; set; } = DateTime.Today;
    public string RecipientName { get; set; } = "Sr.";
    public string RecipientCompany { get; set; } = "";
    public string Reference { get; set; } = "Presupuesto Instalaciones Eléctricas";
    public string ProjectDuration { get; set; } = "18 meses";
    public int ValidityDays { get; set; } = 30;
    public bool IncludeIvaNote { get; set; } = true;
    public bool UseManualNetAmountUf { get; set; }
    public decimal ManualNetAmountUf { get; set; }

    public string ScopeText { get; set; } = "Suministro y montaje de tableros eléctricos.\r\nSuministro y montaje de alimentadores generales y distribución.\r\nSuministro y montaje de escalerillas y bandejas porta conductores.\r\nMontaje de equipos de iluminación.\r\nCanalización y cableado de iluminación, enchufes, fuerza, computación y climatización.\r\nCanalización y enlauchado de sistemas de corrientes débiles según proyecto.";
    public string ExclusionsText { get; set; } = "No se incluyen pagos a la compañía distribuidora, empalmes o equipos de medida no indicados.\r\nNo se incluyen obras civiles, excavaciones, retapes, pintura, cámaras, bases, calados ni compactados.\r\nNo se incluyen equipos de iluminación salvo que estén indicados expresamente en la oferta.\r\nNo se consideran circuitos no proyectados en planos, cuadros de carga o diagramas unilineales.";
    public string CommercialConditionsText { get; set; } = "Monto: de acuerdo con valor neto indicado en oferta comercial.\r\nImpuestos: se debe agregar 19% IVA cuando corresponda.\r\nMoneda del contrato: Unidades de Fomento.\r\nAnticipo: 15% contado.\r\nSaldo: estados de pago mensuales conforme a avance de obra.\r\nRetenciones: 5% de cada estado de pago hasta enterar el porcentaje acordado.";
    public string ObservationsText { get; set; } = "Las marcas indicadas podrán ser reemplazadas por equivalente técnico, previa aprobación de la ITO o mandante. La oferta considera ejecución conforme a planos y antecedentes recibidos.";

    public bool IncludePresentationLetter { get; set; } = true;
    public string PresentationLetterTitle { get; set; } = "CARTA DE PRESENTACIÓN";
    public string PresentationLetterText { get; set; } = "Estimados señores:\r\n\r\nJunto con saludar, presentamos nuestra oferta técnica y comercial para la ejecución de las instalaciones eléctricas del proyecto indicado. La presente propuesta considera los antecedentes recibidos, planos, especificaciones técnicas y condiciones comerciales detalladas en este documento.\r\n\r\nEl alcance, exclusiones, condiciones de pago, duración, marcas y listado de planos forman parte integral de esta oferta. Cualquier modificación de proyecto, aumento o disminución de obra deberá ser formalizada mediante orden de cambio aprobada por las partes.";

    public string SignatureName { get; set; } = "";
    public string SignatureRut { get; set; } = "";
    public string SignatureRole { get; set; } = "";
    public string SignatureRegistroSec { get; set; } = "";
    public string SignaturePhone { get; set; } = "";
    public string SignatureEmail { get; set; } = "";
    public string SignatureImagePath { get; set; } = "";

    public List<OfferPlanReferenceRow> Planos { get; set; } = new();
    public List<OfferProductSpecificationRow> Productos { get; set; } = new();
}
