using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public sealed class DashboardTemplateCardVm
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string NormaSec { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "";
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
    public ICommand? UseTemplateCommand { get; init; }
}

public static class DashboardLandingCoordinator
{
    public static IEnumerable<DashboardTemplateCardVm> CreateQuickTemplates(IShellNavigationHost shell)
    {
        yield return new DashboardTemplateCardVm
        {
            Id = "residential_building",
            Title = "Edificio Residencial",
            Subtitle = "RIC N° 01 al 10",
            NormaSec = "Vivienda Colectiva",
            Description = "Empalmes concentrados, shaft vertical, departamentos tipo y servicios comunes.",
            Icon = "🏢",
            AccentBrush = DashboardKpiBrushes.Blue,
            AccentSoftBrush = DashboardKpiBrushes.BlueSoft,
            UseTemplateCommand = new RelayCommand(() => _ = shell.NewProjectWithTemplateAsync("residential_building"))
        };

        yield return new DashboardTemplateCardVm
        {
            Id = "industrial_warehouse",
            Title = "Galpon Industrial",
            Subtitle = "RIC N° 02, 04, 10",
            NormaSec = "Fuerza Motriz",
            Description = "Subestacion, tableros de fuerza, bandejas portacables y fuerza motriz.",
            Icon = "🏭",
            AccentBrush = DashboardKpiBrushes.Amber,
            AccentSoftBrush = DashboardKpiBrushes.AmberSoft,
            UseTemplateCommand = new RelayCommand(() => _ = shell.NewProjectWithTemplateAsync("industrial_warehouse"))
        };

        yield return new DashboardTemplateCardVm
        {
            Id = "commercial_retail",
            Title = "Local Comercial",
            Subtitle = "RIC N° 02, 08, 10",
            NormaSec = "Retail & Clientes",
            Description = "Alumbrado de emergencia, clima, climatizacion y tableros comerciales.",
            Icon = "🏬",
            AccentBrush = DashboardKpiBrushes.Green,
            AccentSoftBrush = DashboardKpiBrushes.GreenSoft,
            UseTemplateCommand = new RelayCommand(() => _ = shell.NewProjectWithTemplateAsync("commercial_retail"))
        };

        yield return new DashboardTemplateCardVm
        {
            Id = "solar_pv_plant",
            Title = "Planta Solar FV",
            Subtitle = "RIC N° 19, 06",
            NormaSec = "ERNC / Solar",
            Description = "Inversores, arreglos DC/AC, protecciones y malla de puesta a tierra profunda.",
            Icon = "☀️",
            AccentBrush = DashboardKpiBrushes.ForAccent(DashboardExecutiveKpiAccent.Orange),
            AccentSoftBrush = DashboardKpiBrushes.ForAccentSoft(DashboardExecutiveKpiAccent.Orange),
            UseTemplateCommand = new RelayCommand(() => _ = shell.NewProjectWithTemplateAsync("solar_pv_plant"))
        };
    }

    /// <summary>
    /// Presentación del estado del archivo en la lista de recientes: chip, motivo y color.
    /// Un archivo ausente manda por sobre el semáforo de avance, porque el usuario debe saberlo
    /// antes de hacer doble clic y comerse el error.
    /// </summary>
    public static (string Text, string Caption, Brush Accent, Brush Soft) DescribeArchiveState(
        bool isMissing, DashboardProjectStateKind state)
    {
        if (isMissing)
        {
            return ("Archivo no encontrado",
                    "La ruta ya no existe: quítalo de recientes o devuélvelo a su carpeta",
                    DashboardKpiBrushes.Amber, DashboardKpiBrushes.AmberSoft);
        }

        return state switch
        {
            DashboardProjectStateKind.Complete =>
                ("Completo", "Listo para cerrar", DashboardKpiBrushes.Green, DashboardKpiBrushes.GreenSoft),
            DashboardProjectStateKind.InProgress =>
                ("En progreso", "En desarrollo", DashboardKpiBrushes.Amber, DashboardKpiBrushes.AmberSoft),
            DashboardProjectStateKind.Draft =>
                ("Borrador", "Pendiente de trabajo", DashboardKpiBrushes.Blue, DashboardKpiBrushes.BlueSoft),
            _ =>
                ("Sin estado", "Estado no disponible", DashboardKpiBrushes.BlueDark, DashboardKpiBrushes.BlueSoft)
        };
    }

    public static void OpenProjectFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                else
                    WpfDialogService.Warning("Ubicacion de archivo", $"No se encontro la carpeta del archivo:\n{path}");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Dashboard", "Error al abrir la carpeta en el Explorador", ex);
        }
    }

    public static void CopyProjectPathToClipboard(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            Clipboard.SetText(path);
            ShellNotificationService.Instance.ShowSuccess("Ruta copiada", "Ruta copiada al portapapeles con exito.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Dashboard", "Error al copiar la ruta al portapapeles", ex);
        }
    }

    public static void TogglePin(RecentProjectsService recentProjectsService, string? path, Action reloadLandingState)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        bool isPinned = recentProjectsService.TogglePin(path);
        reloadLandingState();
        if (isPinned)
            ShellNotificationService.Instance.ShowSuccess("Proyecto fijado", "El proyecto se mantendra anclado en la parte superior.");
        else
            ShellNotificationService.Instance.ShowInfo("Proyecto desanclado", "El proyecto se ordenara segun fecha reciente.");
    }

    public static void RemoveRecent(RecentProjectsService recentProjectsService, IShellNavigationHost shell, string? path, Action reloadLandingState)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        recentProjectsService.RemoveProject(path);
        shell.RemoveRecentProject(path);
        reloadLandingState();
        ShellNotificationService.Instance.ShowInfo("Proyecto quitado", "El archivo fue eliminado de la lista de recientes.");
    }
}
