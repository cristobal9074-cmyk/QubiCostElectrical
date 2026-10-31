using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Project;

public sealed class ProjectAuditEntryVm
{
    public ProjectAuditEntryVm(ProjectAuditEntryRow row)
    {
        OccurredAtLabel = row.OccurredAt == default ? "" : row.OccurredAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        UserDisplayName = string.IsNullOrWhiteSpace(row.UserDisplayName) ? row.UserEmail : row.UserDisplayName;
        MachineName = row.MachineName;
        Action = row.Action;
        Detail = string.IsNullOrWhiteSpace(row.Detail)
            ? string.Join(" · ", new[] { row.Entity, row.Field, row.NewValue }.Where(s => !string.IsNullOrWhiteSpace(s)))
            : row.Detail;
    }

    public string OccurredAtLabel { get; }
    public string UserDisplayName { get; }
    public string MachineName { get; }
    public string Action { get; }
    public string Detail { get; }

    public string UserInitials
    {
        get
        {
            var name = UserDisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpperInvariant() : parts[0].ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }
    }

    public bool IsApertura => Action.Contains("Abrir", StringComparison.OrdinalIgnoreCase) || Action.Contains("Apertura", StringComparison.OrdinalIgnoreCase) || Action.Contains("Open", StringComparison.OrdinalIgnoreCase);
    public bool IsGuardado => Action.Contains("Guardar", StringComparison.OrdinalIgnoreCase) || Action.Contains("Guardado", StringComparison.OrdinalIgnoreCase) || Action.Contains("Save", StringComparison.OrdinalIgnoreCase);
    public bool IsAjuste => !IsApertura && !IsGuardado;
    public string ActionCategory => IsApertura ? "Apertura" : (IsGuardado ? "Guardado" : "Ajuste");
}

public sealed partial class ProjectViewModel : ViewModelBase, IPageWithPendingChanges, IPageWithCommitChanges, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly ProjectAuditRecorder audit;
    private readonly IPermissionService permissions;
    private readonly IProjectEditWorkspaceService projectEdit;
    private readonly IUserPreferencesStore preferencesStore;
    private ProjectEditSnapshot snapshot = new();
    private ProjectEditSnapshot? baseline;
    private string validationSummary = "";
    private bool showEmptyState = true;
    private string selectedSection = "proyectofinanzas";
}
