using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QubiCostElectrical.Wpf.Features.Delivery;

public sealed partial class OfferSettingsEditorVm : ObservableObject
{
    private readonly BudgetProjectSession _session;

    public OfferSettingsEditorVm(BudgetProjectSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    [ObservableProperty] private string companyName = "";
    [ObservableProperty] private string companyRut = "";
    [ObservableProperty] private string companyAddress = "";
    [ObservableProperty] private string companyPhone = "";
    [ObservableProperty] private string companyEmail = "";
    [ObservableProperty] private string companyWebsite = "";
    [ObservableProperty] private string logoPath = "";
    [ObservableProperty] private string offerNumber = "";
    [ObservableProperty] private string revisionTag = "Rev 0";
    [ObservableProperty] private string offerTitle = "PRESUPUESTO INSTALACIONES ELECTRICAS";
    [ObservableProperty] private DateTime offerDate = DateTime.Today;
    [ObservableProperty] private string recipientName = "Sr.";
    [ObservableProperty] private string recipientCompany = "";
    [ObservableProperty] private string reference = "Presupuesto Instalaciones Electricas";
    [ObservableProperty] private string projectDuration = "18 meses";
    [ObservableProperty] private int validityDays = 30;
    [ObservableProperty] private bool includeIvaNote = true;
    [ObservableProperty] private string scopeText = "...";
    [ObservableProperty] private string exclusionsText = "...";
    [ObservableProperty] private string commercialConditionsText = "...";
    [ObservableProperty] private string observationsText = "...";
    [ObservableProperty] private bool includePresentationLetter = true;
    [ObservableProperty] private string presentationLetterTitle = "CARTA DE PRESENTACION";
    [ObservableProperty] private string presentationLetterText = "...";
    [ObservableProperty] private string signatureName = "";
    [ObservableProperty] private string signatureRut = "";
    [ObservableProperty] private string signatureRole = "";
    [ObservableProperty] private string signatureRegistroSec = "";
    [ObservableProperty] private string signaturePhone = "";
    [ObservableProperty] private string signatureEmail = "";
    [ObservableProperty] private string signatureImagePath = "";

    public void LoadFromSession()
    {
        var settings = _session.OfferPdfSettings;
        CompanyName = settings.CompanyName;
        CompanyRut = settings.CompanyRut;
        CompanyAddress = settings.CompanyAddress;
        CompanyPhone = settings.CompanyPhone;
        CompanyEmail = settings.CompanyEmail;
        CompanyWebsite = settings.CompanyWebsite;
        LogoPath = settings.LogoPath;
        OfferNumber = settings.OfferNumber;
        RevisionTag = settings.RevisionTag;
        OfferTitle = settings.OfferTitle;
        OfferDate = settings.OfferDate;
        RecipientName = settings.RecipientName;
        RecipientCompany = settings.RecipientCompany;
        Reference = settings.Reference;
        ProjectDuration = settings.ProjectDuration;
        ValidityDays = settings.ValidityDays;
        IncludeIvaNote = settings.IncludeIvaNote;
        ScopeText = settings.ScopeText;
        ExclusionsText = settings.ExclusionsText;
        CommercialConditionsText = settings.CommercialConditionsText;
        ObservationsText = settings.ObservationsText;
        IncludePresentationLetter = settings.IncludePresentationLetter;
        PresentationLetterTitle = settings.PresentationLetterTitle;
        PresentationLetterText = settings.PresentationLetterText;
        SignatureName = settings.SignatureName;
        SignatureRut = settings.SignatureRut;
        SignatureRole = settings.SignatureRole;
        SignatureRegistroSec = settings.SignatureRegistroSec;
        SignaturePhone = settings.SignaturePhone;
        SignatureEmail = settings.SignatureEmail;
        SignatureImagePath = settings.SignatureImagePath;
    }

    public void ApplyToSession()
    {
        var settings = _session.OfferPdfSettings;
        settings.CompanyName = CompanyName;
        settings.CompanyRut = CompanyRut;
        settings.CompanyAddress = CompanyAddress;
        settings.CompanyPhone = CompanyPhone;
        settings.CompanyEmail = CompanyEmail;
        settings.CompanyWebsite = CompanyWebsite;
        settings.LogoPath = LogoPath;
        settings.OfferNumber = OfferNumber;
        settings.RevisionTag = RevisionTag;
        settings.OfferTitle = OfferTitle;
        settings.OfferDate = OfferDate;
        settings.RecipientName = RecipientName;
        settings.RecipientCompany = RecipientCompany;
        settings.Reference = Reference;
        settings.ProjectDuration = ProjectDuration;
        settings.ValidityDays = ValidityDays;
        settings.IncludeIvaNote = IncludeIvaNote;
        settings.ScopeText = ScopeText;
        settings.ExclusionsText = ExclusionsText;
        settings.CommercialConditionsText = CommercialConditionsText;
        settings.ObservationsText = ObservationsText;
        settings.IncludePresentationLetter = IncludePresentationLetter;
        settings.PresentationLetterTitle = PresentationLetterTitle;
        settings.PresentationLetterText = PresentationLetterText;
        settings.SignatureName = SignatureName;
        settings.SignatureRut = SignatureRut;
        settings.SignatureRole = SignatureRole;
        settings.SignatureRegistroSec = SignatureRegistroSec;
        settings.SignaturePhone = SignaturePhone;
        settings.SignatureEmail = SignatureEmail;
        settings.SignatureImagePath = SignatureImagePath;
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
        settings.OfferNumber = OfferNumber;
        settings.RevisionTag = RevisionTag;
        settings.OfferTitle = OfferTitle;
        settings.OfferDate = OfferDate;
        settings.RecipientName = RecipientName;
        settings.RecipientCompany = RecipientCompany;
        settings.Reference = Reference;
        settings.ProjectDuration = ProjectDuration;
        settings.ValidityDays = ValidityDays;
        settings.IncludeIvaNote = IncludeIvaNote;
        settings.ScopeText = ScopeText;
        settings.ExclusionsText = ExclusionsText;
        settings.CommercialConditionsText = CommercialConditionsText;
        settings.ObservationsText = ObservationsText;
        settings.IncludePresentationLetter = IncludePresentationLetter;
        settings.PresentationLetterTitle = PresentationLetterTitle;
        settings.PresentationLetterText = PresentationLetterText;
        settings.SignatureName = SignatureName;
        settings.SignatureRut = SignatureRut;
        settings.SignatureRole = SignatureRole;
        settings.SignatureRegistroSec = SignatureRegistroSec;
        settings.SignaturePhone = SignaturePhone;
        settings.SignatureEmail = SignatureEmail;
        settings.SignatureImagePath = SignatureImagePath;
    }
}
