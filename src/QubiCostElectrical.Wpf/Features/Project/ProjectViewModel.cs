using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
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
}

public sealed partial class ProjectViewModel : ViewModelBase, IPageWithPendingChanges, IPageWithCommitChanges, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private ProjectEditSnapshot snapshot = new();
    private ProjectEditSnapshot? baseline;
    private string validationSummary = "";
    private bool showEmptyState = true;
    private string selectedSection = "identidad";
}
