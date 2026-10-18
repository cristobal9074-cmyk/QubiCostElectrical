#nullable enable
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Wpf.Shell;

public sealed record ChangelogItem(string Text);

public sealed record ChangelogVersion(
    string Version,
    string Date,
    string Tagline,
    bool IsCurrent,
    IReadOnlyList<ChangelogItem> Highlights);

public sealed class AboutViewModel : ObservableObject
{
    private readonly ILicenseService licenseService;
    private int selectedTabIndex;
    private string licenseMessage = string.Empty;
    private bool isLicenseMessageError;

    public AboutViewModel(ILicenseService? licenseService = null)
    {
        this.licenseService = licenseService ?? new LicenseService();
        RefreshLicenseInfo();

        ActivateLicenseFileCommand = new RelayCommand(ActivateLicenseFile);

        Changelog = new List<ChangelogVersion>
        {
            new(
                "v1.1.1",
                "Octubre 2026",
                "Experiencia ejecutiva, microinteracciones y ergonomía",
                true,
                new List<ChangelogItem>
                {
                    new("Hero KPI Bar en Itemizado: métricas directas y oferta en CLP/UF, barra de distribución de costos (Materiales vs MO) y filtro rápido de salud de partidas."),
                    new("Barra de Estado Enriquecida (Status Bar): selector interactivo CLP/UF, ticker de costo directo/oferta, indicador de red y atajos F1/Ctrl+K."),
                    new("Atajos de Teclado F1: visualización moderna con Kbd Chips estilizados y buscador reactivo instantáneo."),
                    new("Microinteracciones y feedback de hover/focus refinados en tablas y botones principales."),
                    new("Validación de balance comercial y optimización de rendimiento en grillas de cubicación.")
                }),
            new(
                "v1.1.0",
                "Septiembre 2026",
                "Navegación por ciclo de vida, cotizaciones y colaboración",
                false,
                new List<ChangelogItem>
                {
                    new("Selector de ciclo de vida de proyecto: Presupuesto, Planificación y Ejecución."),
                    new("Módulo de Solicitudes de Cotización a proveedores con vista previa interactiva y exportación Excel/PDF."),
                    new("Colaboración por archivo en red compartida (SMB/UNC) con bloqueo cooperativo y auditoría persistente.")
                }),
            new(
                "v1.0.0",
                "Agosto 2026",
                "Lanzamiento de plataforma moderna WPF y motor SQLite",
                false,
                new List<ChangelogItem>
                {
                    new("Motor de presupuestos eléctricos de alto rendimiento basado en SQLite (.qce)."),
                    new("Toma de cantidades y cubicación por recintos, circuitos y tableros."),
                    new("Exportador corporativo de cartas de oferta, resúmenes e informes técnicos en PDF y Excel.")
                })
        };
    }

    public string ProductName => ProductIdentity.ProductName;
    public string ProductSubtitle => "Software Profesional de Presupuestos y Cubicaciones Eléctricas";
    public string VersionLabel => ProductVersionInfo.ProductVersionLabel;
    public string ProjectFormatLabel => ProductVersionInfo.ProjectFormatLabel;
    public string CalculationEngineLabel => ProductVersionInfo.CalculationEngineLabel;
    public string DatabaseSchemaLabel => $"Esquema v{ProductVersionInfo.DatabaseSchemaVersion}";
    public string RuntimeLabel => ".NET 10.0 (win-x64) · WPF Modern Desktop";
    public string CopyrightText => "© 2026 Qubi Cost SpA. Todos los derechos reservados.";

    public int SelectedTabIndex
    {
        get => selectedTabIndex;
        set => SetProperty(ref selectedTabIndex, value);
    }

    public string LicenseStatusBadge { get; private set; } = string.Empty;
    public string LicenseStatusBadgeBackground { get; private set; } = "#E0E7FF";
    public string LicenseStatusBadgeForeground { get; private set; } = "#1D4ED8";
    public string LicenseeCompany { get; private set; } = string.Empty;
    public string LicenseeRut { get; private set; } = string.Empty;
    public string LicenseExpirationText { get; private set; } = string.Empty;
    public bool IsLicenseValid { get; private set; }

    public string LicenseMessage
    {
        get => licenseMessage;
        private set
        {
            if (SetProperty(ref licenseMessage, value))
                OnPropertyChanged(nameof(HasLicenseMessage));
        }
    }

    public bool HasLicenseMessage => !string.IsNullOrWhiteSpace(licenseMessage);

    public bool IsLicenseMessageError
    {
        get => isLicenseMessageError;
        private set => SetProperty(ref isLicenseMessageError, value);
    }

    public IReadOnlyList<ChangelogVersion> Changelog { get; }

    public IRelayCommand ActivateLicenseFileCommand { get; }

    public void RefreshLicenseInfo()
    {
        var status = licenseService.Status;
        var info = licenseService.CurrentLicense;

        switch (status)
        {
            case LicenseStatus.Valid:
                LicenseStatusBadge = "LICENCIA COMERCIAL VÁLIDA";
                LicenseStatusBadgeBackground = "#DCFCE7";
                LicenseStatusBadgeForeground = "#15803D";
                LicenseeCompany = info?.Company ?? licenseService.LicenseeCompanyName;
                LicenseeRut = string.IsNullOrWhiteSpace(info?.Rut) ? "No especificado" : info.Rut;
                LicenseExpirationText = info != null ? info.ExpiresAt.ToString("dd/MM/yyyy") : "Vigente";
                IsLicenseValid = true;
                break;

            case LicenseStatus.GracePeriod:
                LicenseStatusBadge = "PERIODO DE GRACIA";
                LicenseStatusBadgeBackground = "#FEF3C7";
                LicenseStatusBadgeForeground = "#B45309";
                LicenseeCompany = info?.Company ?? licenseService.LicenseeCompanyName;
                LicenseeRut = info?.Rut ?? "—";
                LicenseExpirationText = info != null ? $"Expira el {info.ExpiresAt:dd/MM/yyyy}" : "Por renovar";
                IsLicenseValid = true;
                break;

            case LicenseStatus.Expired:
                LicenseStatusBadge = "LICENCIA EXPIRADA";
                LicenseStatusBadgeBackground = "#FEE2E2";
                LicenseStatusBadgeForeground = "#B91C1C";
                LicenseeCompany = info?.Company ?? licenseService.LicenseeCompanyName;
                LicenseeRut = info?.Rut ?? "—";
                LicenseExpirationText = info != null ? $"Expiró el {info.ExpiresAt:dd/MM/yyyy}" : "Expirada";
                IsLicenseValid = false;
                break;

            case LicenseStatus.Invalid:
                LicenseStatusBadge = "LICENCIA INVÁLIDA";
                LicenseStatusBadgeBackground = "#FEE2E2";
                LicenseStatusBadgeForeground = "#B91C1C";
                LicenseeCompany = "Firma no válida";
                LicenseeRut = "—";
                LicenseExpirationText = "No aplicable";
                IsLicenseValid = false;
                break;

            case LicenseStatus.NotActivated:
            default:
                LicenseStatusBadge = "MODO EVALUACIÓN / COMUNITARIO";
                LicenseStatusBadgeBackground = "#F1F5F9";
                LicenseStatusBadgeForeground = "#475569";
                LicenseeCompany = "Licencia no activada";
                LicenseeRut = "—";
                LicenseExpirationText = "Uso de evaluación";
                IsLicenseValid = false;
                break;
        }

        OnPropertyChanged(nameof(LicenseStatusBadge));
        OnPropertyChanged(nameof(LicenseStatusBadgeBackground));
        OnPropertyChanged(nameof(LicenseStatusBadgeForeground));
        OnPropertyChanged(nameof(LicenseeCompany));
        OnPropertyChanged(nameof(LicenseeRut));
        OnPropertyChanged(nameof(LicenseExpirationText));
        OnPropertyChanged(nameof(IsLicenseValid));
    }

    private void ActivateLicenseFile()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Seleccionar archivo de licencia de Qubi Cost Electrical",
            Filter = "Archivo de Licencia (*.qclic)|*.qclic|Todos los archivos (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog() == true)
        {
            var result = licenseService.Activate(ofd.FileName);
            if (result.IsSuccess)
            {
                RefreshLicenseInfo();
                IsLicenseMessageError = false;
                LicenseMessage = "¡Licencia activada exitosamente!";
            }
            else
            {
                IsLicenseMessageError = true;
                LicenseMessage = $"Error al activar licencia: {result.Error}";
            }
        }
    }
}
