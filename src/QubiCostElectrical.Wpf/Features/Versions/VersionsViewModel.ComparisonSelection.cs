using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed partial class VersionsViewModel
{
    private static readonly ComparisonTargetOption InitialCurrentStateOption = ComparisonTargetOption.CreateCurrentState();

    private readonly ObservableCollection<ComparisonTargetOption> targetVersionOptions = new()
    {
        InitialCurrentStateOption
    };
    public ObservableCollection<ComparisonTargetOption> TargetVersionOptions => targetVersionOptions;

    private ComparisonTargetOption? selectedTargetOption = InitialCurrentStateOption;
    private bool isSyncingTargetOptions;
    private RelayCommand? swapVersionsCommand;

    public ComparisonTargetOption? SelectedTargetOption
    {
        get => selectedTargetOption;
        set
        {
            if (isSyncingTargetOptions || Equals(selectedTargetOption, value) || value == null)
                return;

            selectedTargetOption = value;
            OnPropertyChanged(nameof(SelectedTargetOption));

            if (value.IsCurrentState)
            {
                compareAgainstCurrent = true;
                selectedComparisonVersion = null;
            }
            else
            {
                compareAgainstCurrent = false;
                selectedComparisonVersion = value.Version;
            }

            OnPropertyChanged(nameof(CompareAgainstCurrent));
            OnPropertyChanged(nameof(ShowComparisonVersionSelector));
            OnPropertyChanged(nameof(SelectedComparisonVersion));
            NotifyTargetPropertiesChanged();
            RefreshComparison();
        }
    }

    public bool CanSwapVersions =>
        !CompareAgainstCurrent &&
        SelectedVersion != null &&
        SelectedComparisonVersion != null &&
        !string.Equals(SelectedVersion.Source.Id, SelectedComparisonVersion.Source.Id, StringComparison.OrdinalIgnoreCase);

    public ICommand SwapVersionsCommand => swapVersionsCommand ??= new RelayCommand(SwapVersions, () => CanSwapVersions);

    public string ComparisonBaselineVersionLabel => SelectedVersion != null
        ? $"{SelectedVersion.Codigo} · {SelectedVersion.Nombre}"
        : "—";

    public string ComparisonBaselineVersionSub => SelectedVersion != null
        ? $"{SelectedVersion.FechaModificacion:dd/MM/yyyy HH:mm} · {SelectedVersion.CreadoPor}"
        : "";

    public string ComparisonTargetVersionLabel => CompareAgainstCurrent
        ? "Estado actual"
        : (SelectedComparisonVersion != null ? $"{SelectedComparisonVersion.Codigo} · {SelectedComparisonVersion.Nombre}" : "—");

    public string ComparisonTargetVersionSub => CompareAgainstCurrent
        ? "Presupuesto activo en curso"
        : (SelectedComparisonVersion != null ? $"{SelectedComparisonVersion.FechaModificacion:dd/MM/yyyy HH:mm} · {SelectedComparisonVersion.CreadoPor}" : "");

    public string ComparisonTargetTotalText => ComparisonCurrentTotalText;
    public string ComparisonTargetCountText => ComparisonCurrentCountText;

    private void SwapVersions()
    {
        if (!CanSwapVersions) return;
        var oldA = SelectedVersion;
        var oldB = SelectedComparisonVersion;
        if (oldA == null || oldB == null) return;

        isSyncingTargetOptions = true;
        try
        {
            selectedVersion = oldB;
            selectedComparisonVersion = oldA;
            OnPropertyChanged(nameof(SelectedVersion));
            OnPropertyChanged(nameof(SelectedComparisonVersion));

            UpdateComparisonVersionOptions();

            selectedTargetOption = targetVersionOptions.FirstOrDefault(o =>
                !o.IsCurrentState && string.Equals(o.Version?.Source.Id, oldA.Source.Id, StringComparison.OrdinalIgnoreCase));
            OnPropertyChanged(nameof(SelectedTargetOption));
        }
        finally
        {
            isSyncingTargetOptions = false;
        }

        RefreshComparison();
        NotifyTargetPropertiesChanged();
    }

    private void NotifyTargetPropertiesChanged()
    {
        swapVersionsCommand?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSwapVersions));
        OnPropertyChanged(nameof(ComparisonBaselineVersionLabel));
        OnPropertyChanged(nameof(ComparisonBaselineVersionSub));
        OnPropertyChanged(nameof(ComparisonTargetVersionLabel));
        OnPropertyChanged(nameof(ComparisonTargetVersionSub));
        OnPropertyChanged(nameof(ComparisonTargetTotalText));
        OnPropertyChanged(nameof(ComparisonTargetCountText));
    }

    partial void OnComparisonVersionOptionsUpdated()
    {
        UpdateTargetVersionOptions();
    }

    partial void OnComparisonSummaryChangedExtra()
    {
        NotifyTargetPropertiesChanged();
    }

    partial void OnCompareAgainstCurrentChanged()
    {
        if (isSyncingTargetOptions) return;

        isSyncingTargetOptions = true;
        try
        {
            if (compareAgainstCurrent)
            {
                selectedTargetOption = targetVersionOptions.FirstOrDefault(o => o.IsCurrentState);
                selectedComparisonVersion = null;
            }
            else
            {
                selectedTargetOption = targetVersionOptions.FirstOrDefault(o =>
                    !o.IsCurrentState && o.Version?.Source.Id != SelectedVersion?.Source.Id)
                    ?? targetVersionOptions.FirstOrDefault(o => !o.IsCurrentState);
                selectedComparisonVersion = selectedTargetOption?.Version;
            }

            OnPropertyChanged(nameof(SelectedTargetOption));
            OnPropertyChanged(nameof(SelectedComparisonVersion));
        }
        finally
        {
            isSyncingTargetOptions = false;
        }

        NotifyTargetPropertiesChanged();
    }

    partial void OnSelectedComparisonVersionChanged()
    {
        if (isSyncingTargetOptions) return;

        isSyncingTargetOptions = true;
        try
        {
            if (compareAgainstCurrent || selectedComparisonVersion == null)
            {
                selectedTargetOption = targetVersionOptions.FirstOrDefault(o => o.IsCurrentState);
            }
            else
            {
                selectedTargetOption = targetVersionOptions.FirstOrDefault(o =>
                    !o.IsCurrentState && string.Equals(o.Version?.Source.Id, selectedComparisonVersion.Source.Id, StringComparison.OrdinalIgnoreCase))
                    ?? targetVersionOptions.FirstOrDefault(o => !o.IsCurrentState);
            }

            OnPropertyChanged(nameof(SelectedTargetOption));
        }
        finally
        {
            isSyncingTargetOptions = false;
        }

        NotifyTargetPropertiesChanged();
    }

    private void UpdateTargetVersionOptions()
    {
        isSyncingTargetOptions = true;
        try
        {
            string? previousVersionId = selectedComparisonVersion?.Source.Id;
            bool wasCurrent = compareAgainstCurrent || selectedTargetOption == null || selectedTargetOption.IsCurrentState;

            targetVersionOptions.Clear();

            // Opción 1: Estado actual en curso
            var currentOpt = ComparisonTargetOption.CreateCurrentState();
            targetVersionOptions.Add(currentOpt);

            // Opciones 2..N: Todas las versiones históricas existentes
            foreach (var v in versions)
            {
                targetVersionOptions.Add(ComparisonTargetOption.FromVersion(v));
            }

            if (wasCurrent)
            {
                selectedTargetOption = currentOpt;
                compareAgainstCurrent = true;
                selectedComparisonVersion = null;
            }
            else
            {
                var match = targetVersionOptions.FirstOrDefault(o =>
                    !o.IsCurrentState && string.Equals(o.Version?.Source.Id, previousVersionId, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    selectedTargetOption = match;
                    selectedComparisonVersion = match.Version;
                    compareAgainstCurrent = false;
                }
                else
                {
                    var firstOther = targetVersionOptions.FirstOrDefault(o =>
                        !o.IsCurrentState && o.Version?.Source.Id != SelectedVersion?.Source.Id)
                        ?? targetVersionOptions.FirstOrDefault(o => !o.IsCurrentState);

                    if (firstOther != null)
                    {
                        selectedTargetOption = firstOther;
                        selectedComparisonVersion = firstOther.Version;
                        compareAgainstCurrent = false;
                    }
                    else
                    {
                        selectedTargetOption = currentOpt;
                        compareAgainstCurrent = true;
                        selectedComparisonVersion = null;
                    }
                }
            }

            OnPropertyChanged(nameof(SelectedTargetOption));
            OnPropertyChanged(nameof(CompareAgainstCurrent));
            OnPropertyChanged(nameof(ShowComparisonVersionSelector));
            OnPropertyChanged(nameof(SelectedComparisonVersion));
        }
        finally
        {
            isSyncingTargetOptions = false;
        }

        NotifyTargetPropertiesChanged();
    }
}
