using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QubiCostElectrical.Wpf.Features.Delivery;

public sealed partial class OfferSettingsEditorVm : ObservableObject
{
    private readonly BudgetProjectSession _session;
    private readonly Func<bool>? canEdit;
    private readonly Func<bool>? canManageGlobals;

    public OfferSettingsEditorVm(BudgetProjectSession session, Func<bool>? canEdit = null, Func<bool>? canManageGlobals = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        this.canEdit = canEdit;
        this.canManageGlobals = canManageGlobals;
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public bool CanEdit => AllowEdit;
    public bool IsReadOnly => !AllowEdit;

    /// <summary>Puede escribir preferencias globales (rol Administrador).</summary>
    public bool CanManageGlobals => canManageGlobals?.Invoke() ?? true;

    public void NotifyEditAccessChanged()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanManageGlobals));
    }

    [ObservableProperty] private string companyName = "";
    [ObservableProperty] private string companyRut = "";
    [ObservableProperty] private string companyAddress = "";
    [ObservableProperty] private string companyPhone = "";
    [ObservableProperty] private string companyEmail = "";
    [ObservableProperty] private string companyWebsite = "";
    [ObservableProperty] private string logoPath = "";
    [ObservableProperty] private string clientLogoPath = "";
    [ObservableProperty] private string offerNumber = "";
    [ObservableProperty] private string revisionTag = "Rev 0";
    [ObservableProperty] private string offerTitle = "PRESUPUESTO INSTALACIONES ELÉCTRICAS";
    [ObservableProperty] private DateTime offerDate = DateTime.Today;
    [ObservableProperty] private string recipientName = "Sr.";
    [ObservableProperty] private string recipientCompany = "";
    [ObservableProperty] private string reference = "Presupuesto Instalaciones Eléctricas";
    [ObservableProperty] private string montajeText = "Instalaciones Eléctricas y Canalización de corrientes débiles.";
    [ObservableProperty] private string planosHint = "Si esta lista está vacía, la carta usa los planos que subiste en Antecedentes. Cada especialidad (eléctricos, CCDD, teleco) sale solo si tiene planos.";
    [ObservableProperty] private string projectDuration = "18 meses";
    [ObservableProperty] private int validityDays = 30;
    [ObservableProperty] private bool includeIvaNote = true;
    [ObservableProperty] private bool showUnitPricesInItemizado = true;
    [ObservableProperty] private bool showCostComponentDistribution = true;
    [ObservableProperty] private string scopeText = "...";
    [ObservableProperty] private string exclusionsText = "...";
    [ObservableProperty] private string commercialConditionsText = "...";
    [ObservableProperty] private string normativeAssumptionsText = "...";
    [ObservableProperty] private string observationsText = "...";
    [ObservableProperty] private bool includeEquipmentBrands = true;
    [ObservableProperty] private string equipmentBrandsText = "...";
    [ObservableProperty] private bool includeDrawingList = true;
    [ObservableProperty] private bool includePresentationLetter = true;
    [ObservableProperty] private string presentationLetterTitle = "CARTA DE PRESENTACIÓN";
    [ObservableProperty] private string presentationLetterText = "...";
    [ObservableProperty] private string signatureName = "";
    [ObservableProperty] private string signatureRut = "";
    [ObservableProperty] private string signatureRole = "";
    [ObservableProperty] private string signatureRegistroSec = "";
    [ObservableProperty] private string signatureSecClass = "Clase A";
    [ObservableProperty] private string signaturePhone = "";
    [ObservableProperty] private string signatureEmail = "";
    [ObservableProperty] private string signatureImagePath = "";
    [ObservableProperty] private int signatureWidthMm = 46;
    [ObservableProperty] private int signatureHeightMm = 16;
    [ObservableProperty] private string defaultsHint = "";

    public void LoadFromSession()
    {
        // Rellena desde las preferencias globales los campos que el proyecto no tiene.
        LetterheadApplicator.Apply(UserPreferencesStore.Load(), _session.OfferPdfSettings);
        var settings = _session.OfferPdfSettings;
        CompanyName = settings.CompanyName;
        CompanyRut = settings.CompanyRut;
        CompanyAddress = settings.CompanyAddress;
        CompanyPhone = settings.CompanyPhone;
        CompanyEmail = settings.CompanyEmail;
        CompanyWebsite = settings.CompanyWebsite;
        LogoPath = settings.LogoPath;
        ClientLogoPath = settings.ClientLogoPath;
        OfferNumber = settings.OfferNumber;
        RevisionTag = settings.RevisionTag;
        OfferTitle = settings.OfferTitle;
        OfferDate = settings.OfferDate;
        RecipientName = settings.RecipientName;
        RecipientCompany = settings.RecipientCompany;
        Reference = settings.Reference;
        MontajeText = string.IsNullOrWhiteSpace(settings.MontajeText)
            ? "Instalaciones Eléctricas y Canalización de corrientes débiles."
            : settings.MontajeText;
        ProjectDuration = settings.ProjectDuration;
        ValidityDays = settings.ValidityDays;
        IncludeIvaNote = settings.IncludeIvaNote;
        ShowUnitPricesInItemizado = settings.ShowUnitPricesInItemizado;
        ShowCostComponentDistribution = settings.ShowCostComponentDistribution;
        ScopeText = settings.ScopeText;
        ExclusionsText = settings.ExclusionsText;
        CommercialConditionsText = settings.CommercialConditionsText;
        NormativeAssumptionsText = settings.NormativeAssumptionsText;
        ObservationsText = settings.ObservationsText;
        IncludeEquipmentBrands = settings.IncludeEquipmentBrands;
        EquipmentBrandsText = settings.EquipmentBrandsText;
        IncludeDrawingList = settings.IncludeDrawingList;
        IncludePresentationLetter = settings.IncludePresentationLetter;
        PresentationLetterTitle = settings.PresentationLetterTitle;
        PresentationLetterText = settings.PresentationLetterText;
        SignatureName = settings.SignatureName;
        SignatureRut = settings.SignatureRut;
        SignatureRole = settings.SignatureRole;
        SignatureRegistroSec = settings.SignatureRegistroSec;
        SignatureSecClass = string.IsNullOrWhiteSpace(settings.SignatureSecClass) ? "Clase A" : settings.SignatureSecClass;
        SignaturePhone = settings.SignaturePhone;
        SignatureEmail = settings.SignatureEmail;
        SignatureImagePath = settings.SignatureImagePath;
        SignatureWidthMm = settings.SignatureWidthMm <= 0 ? 46 : settings.SignatureWidthMm;
        SignatureHeightMm = settings.SignatureHeightMm <= 0 ? 16 : settings.SignatureHeightMm;
        DefaultsHint = "";
        LoadPlanos(settings.Planos);
        LoadMarcas(settings.Productos);
        NotifyEditAccessChanged();
    }

    public void ApplyToSession()
    {
        if (!AllowEdit)
            return;

        var settings = _session.OfferPdfSettings;
        settings.CompanyName = CompanyName;
        settings.CompanyRut = CompanyRut;
        settings.CompanyAddress = CompanyAddress;
        settings.CompanyPhone = CompanyPhone;
        settings.CompanyEmail = CompanyEmail;
        settings.CompanyWebsite = CompanyWebsite;
        settings.LogoPath = LogoPath;
        settings.ClientLogoPath = ClientLogoPath;
        settings.OfferNumber = OfferNumber;
        settings.RevisionTag = RevisionTag;
        settings.OfferTitle = OfferTitle;
        settings.OfferDate = OfferDate;
        settings.RecipientName = RecipientName;
        settings.RecipientCompany = RecipientCompany;
        settings.Reference = Reference;
        settings.MontajeText = MontajeText;
        settings.ProjectDuration = ProjectDuration;
        settings.ValidityDays = ValidityDays;
        settings.IncludeIvaNote = IncludeIvaNote;
        settings.ShowUnitPricesInItemizado = ShowUnitPricesInItemizado;
        settings.ShowCostComponentDistribution = ShowCostComponentDistribution;
        settings.ScopeText = ScopeText;
        settings.ExclusionsText = ExclusionsText;
        settings.CommercialConditionsText = CommercialConditionsText;
        settings.NormativeAssumptionsText = NormativeAssumptionsText;
        settings.ObservationsText = ObservationsText;
        settings.IncludeEquipmentBrands = IncludeEquipmentBrands;
        settings.EquipmentBrandsText = EquipmentBrandsText;
        settings.IncludeDrawingList = IncludeDrawingList;
        settings.IncludePresentationLetter = IncludePresentationLetter;
        settings.PresentationLetterTitle = PresentationLetterTitle;
        settings.PresentationLetterText = PresentationLetterText;
        settings.SignatureName = SignatureName;
        settings.SignatureRut = SignatureRut;
        settings.SignatureRole = SignatureRole;
        settings.SignatureRegistroSec = SignatureRegistroSec;
        settings.SignatureSecClass = SignatureSecClass;
        settings.SignaturePhone = SignaturePhone;
        settings.SignatureEmail = SignatureEmail;
        settings.SignatureImagePath = SignatureImagePath;
        settings.SignatureWidthMm = SignatureWidthMm;
        settings.SignatureHeightMm = SignatureHeightMm;
        settings.Planos = ExportPlanos();
        settings.Productos = ExportProductos();
    }

    // ── Membrete/firma: guardar como predeterminado global ──────────────
    public ICommand SaveAsDefaultsCommand => new RelayCommand(SaveAsDefaults, () => CanManageGlobals);

    private void SaveAsDefaults()
    {
        if (!CanManageGlobals)
            return;

        var prefs = UserPreferencesStore.Load();
        prefs.LetterheadCompanyName = CompanyName;
        prefs.LetterheadRut = CompanyRut;
        prefs.LetterheadAddress = CompanyAddress;
        prefs.LetterheadPhone = CompanyPhone;
        prefs.LetterheadEmail = CompanyEmail;
        prefs.LetterheadWebsite = CompanyWebsite;
        prefs.LetterheadLogoPath = LogoPath;
        prefs.LetterheadClientLogoPath = ClientLogoPath;
        prefs.ProfessionalName = SignatureName;
        prefs.ProfessionalRut = SignatureRut;
        prefs.ProfessionalRole = SignatureRole;
        prefs.ProfessionalRegistroSec = SignatureRegistroSec;
        prefs.ProfessionalSignatureImagePath = SignatureImagePath;
        prefs.ProfessionalPhone = SignaturePhone;
        prefs.ProfessionalEmail = SignatureEmail;
        prefs.ProfessionalSecClass = SignatureSecClass;
        prefs.ProfessionalSignatureWidthMm = SignatureWidthMm;
        prefs.ProfessionalSignatureHeightMm = SignatureHeightMm;
        UserPreferencesStore.Save(prefs);
        DefaultsHint = "Guardado. Los proyectos sin estos datos usarán estos valores.";
    }

    public void CopyTo(OfferPdfSettings settings)
    {
        settings.CompanyName = CompanyName;
        settings.CompanyRut = CompanyRut;
        settings.CompanyAddress = CompanyAddress;
        settings.CompanyPhone = CompanyPhone;
        settings.CompanyEmail = CompanyEmail;
        settings.CompanyWebsite = CompanyWebsite;
        settings.LogoPath = LogoPath;
        settings.ClientLogoPath = ClientLogoPath;
        settings.OfferNumber = OfferNumber;
        settings.RevisionTag = RevisionTag;
        settings.OfferTitle = OfferTitle;
        settings.OfferDate = OfferDate;
        settings.RecipientName = RecipientName;
        settings.RecipientCompany = RecipientCompany;
        settings.Reference = Reference;
        settings.MontajeText = MontajeText;
        settings.ProjectDuration = ProjectDuration;
        settings.ValidityDays = ValidityDays;
        settings.IncludeIvaNote = IncludeIvaNote;
        settings.ShowUnitPricesInItemizado = ShowUnitPricesInItemizado;
        settings.ShowCostComponentDistribution = ShowCostComponentDistribution;
        settings.ScopeText = ScopeText;
        settings.ExclusionsText = ExclusionsText;
        settings.CommercialConditionsText = CommercialConditionsText;
        settings.NormativeAssumptionsText = NormativeAssumptionsText;
        settings.ObservationsText = ObservationsText;
        settings.IncludeEquipmentBrands = IncludeEquipmentBrands;
        settings.EquipmentBrandsText = EquipmentBrandsText;
        settings.IncludeDrawingList = IncludeDrawingList;
        settings.IncludePresentationLetter = IncludePresentationLetter;
        settings.PresentationLetterTitle = PresentationLetterTitle;
        settings.PresentationLetterText = PresentationLetterText;
        settings.SignatureName = SignatureName;
        settings.SignatureRut = SignatureRut;
        settings.SignatureRole = SignatureRole;
        settings.SignatureRegistroSec = SignatureRegistroSec;
        settings.SignatureSecClass = SignatureSecClass;
        settings.SignaturePhone = SignaturePhone;
        settings.SignatureEmail = SignatureEmail;
        settings.SignatureImagePath = SignatureImagePath;
        settings.SignatureWidthMm = SignatureWidthMm;
        settings.SignatureHeightMm = SignatureHeightMm;
        settings.Planos = ExportPlanos();
        settings.Productos = ExportProductos();
    }

    // ══ Planos del proyecto ════════════════════════════════════════════════
    public ObservableCollection<OfferPlanRowVm> Planos { get; } = new();

    [ObservableProperty] private OfferPlanRowVm? selectedPlano;

    public ICommand AddPlanoCommand => new RelayCommand(AddPlano);
    public ICommand RemovePlanoCommand => new RelayCommand(RemovePlano, () => SelectedPlano != null);
    public ICommand ImportUploadedPlanosCommand => new RelayCommand(ImportUploadedPlanos);

    private void ImportUploadedPlanos()
    {
        var rows = OfferLetterPlanCatalog.FromUploads(_session.AntecedentesTecnicos);
        if (rows.Count == 0)
        {
            PlanosHint = "No hay planos vigentes en Antecedentes. Súbelos en Planos y antecedentes, o agrégalos aquí.";
            return;
        }

        Planos.Clear();
        foreach (var p in rows)
        {
            Planos.Add(new OfferPlanRowVm
            {
                Codigo = p.Codigo,
                Revision = p.Revision,
                Descripcion = p.Descripcion,
                Disciplina = p.Disciplina
            });
        }

        IncludeDrawingList = true;
        PlanosHint = $"{rows.Count:N0} planos traídos. Eléctricos, corrientes débiles y teleco salen en secciones distintas. Si no quieres imprimirlos, desmarca el listado.";
    }

    private void AddPlano()
    {
        var row = new OfferPlanRowVm { Disciplina = "Electricidad" };
        Planos.Add(row);
        SelectedPlano = row;
    }

    private void RemovePlano()
    {
        if (SelectedPlano != null)
            Planos.Remove(SelectedPlano);
    }

    private void LoadPlanos(IEnumerable<OfferPlanReferenceRow> source)
    {
        Planos.Clear();
        foreach (var p in source)
            Planos.Add(new OfferPlanRowVm
            {
                Codigo = p.Codigo,
                Revision = p.Revision,
                Descripcion = p.Descripcion,
                Disciplina = p.Disciplina
            });
    }

    private List<OfferPlanReferenceRow> ExportPlanos() =>
        Planos.Select(p => new OfferPlanReferenceRow
        {
            Codigo = p.Codigo,
            Revision = p.Revision,
            Descripcion = p.Descripcion,
            Disciplina = p.Disciplina
        }).ToList();

    // ══ Marcas de equipos ════════════════════════════════════════════════
    public ObservableCollection<OfferBrandRowVm> Marcas { get; } = new();

    [ObservableProperty] private OfferBrandRowVm? selectedMarca;

    public ICommand AddMarcaCommand => new RelayCommand(AddMarca);
    public ICommand RemoveMarcaCommand => new RelayCommand(RemoveMarca, () => SelectedMarca != null);

    private void AddMarca()
    {
        var row = new OfferBrandRowVm { Categoria = "" };
        Marcas.Add(row);
        SelectedMarca = row;
    }

    private void RemoveMarca()
    {
        if (SelectedMarca != null)
            Marcas.Remove(SelectedMarca);
    }

    private void LoadMarcas(IEnumerable<OfferProductSpecificationRow> source)
    {
        Marcas.Clear();
        foreach (var p in source)
            Marcas.Add(new OfferBrandRowVm
            {
                Categoria = p.Categoria,
                Marca = p.Marca,
                Especificacion = p.Especificacion
            });
    }

    private List<OfferProductSpecificationRow> ExportProductos() =>
        Marcas.Select(p => new OfferProductSpecificationRow
        {
            Categoria = p.Categoria,
            Marca = p.Marca,
            Especificacion = p.Especificacion
        }).ToList();
}

/// <summary>Row view-model editable en el DataGrid de planos de la oferta.</summary>
public sealed partial class OfferPlanRowVm : ObservableObject
{
    [ObservableProperty] private string codigo = "";
    [ObservableProperty] private string revision = "";
    [ObservableProperty] private string descripcion = "";
    [ObservableProperty] private string disciplina = "Electricidad";
}

/// <summary>Row view-model editable en el DataGrid de marcas de equipos de la oferta.</summary>
public sealed partial class OfferBrandRowVm : ObservableObject
{
    [ObservableProperty] private string categoria = "";
    [ObservableProperty] private string marca = "";
    [ObservableProperty] private string especificacion = "";
}
