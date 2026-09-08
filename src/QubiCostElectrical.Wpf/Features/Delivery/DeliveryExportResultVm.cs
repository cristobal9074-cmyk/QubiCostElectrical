using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Delivery;

public sealed class DeliveryExportResultItemVm
{
    public DeliveryExportResultItemVm(string documentType, bool isSuccess, string? filePath = null, string? errorMessage = null, FileOpenResult openResult = FileOpenResult.Success)
    {
        DocumentType = documentType;
        IsSuccess = isSuccess;
        FilePath = filePath;
        ErrorMessage = errorMessage;
        OpenResult = openResult;
    }

    public string DocumentType { get; }
    public bool IsSuccess { get; }
    public string? FilePath { get; }
    public string? ErrorMessage { get; }
    public FileOpenResult OpenResult { get; }
}

public sealed class DeliveryExportResultVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeliveryExportResultItemVm> Results { get; } = new();

    public int SuccessCount => Results.Count(r => r.IsSuccess);
    public int FailCount => Results.Count(r => !r.IsSuccess);
    public bool HasResults => Results.Count > 0;
    public bool HasErrors => FailCount > 0;

    public string SummaryText =>
        !HasResults
            ? string.Empty
            : HasErrors
                ? $"{SuccessCount} documento(s) generado(s), {FailCount} con error."
                : $"{SuccessCount} documento(s) generado(s) correctamente.";

    public string? PreviewFilePath { get; private set; }
    public FileOpenResult PreviewOpenResult { get; private set; }

    public void SetPreview(string? filePath, FileOpenResult openResult = FileOpenResult.Success)
    {
        PreviewFilePath = filePath;
        PreviewOpenResult = openResult;
        OnPropertyChanged(nameof(PreviewFilePath));
        OnPropertyChanged(nameof(PreviewOpenResult));
    }

    public void SetResults(IReadOnlyList<DeliveryExportResult> results)
    {
        Results.Clear();
        foreach (var r in results)
            Results.Add(new DeliveryExportResultItemVm(r.DocumentType, r.IsSuccess, r.FilePath, r.ErrorMessage, r.OpenResult));
        RaiseSummaryChanged();
    }

    public void Clear()
    {
        Results.Clear();
        PreviewFilePath = null;
        PreviewOpenResult = FileOpenResult.Success;
        OnPropertyChanged(nameof(PreviewFilePath));
        OnPropertyChanged(nameof(PreviewOpenResult));
        RaiseSummaryChanged();
    }

    private void RaiseSummaryChanged()
    {
        OnPropertyChanged(nameof(SuccessCount));
        OnPropertyChanged(nameof(FailCount));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
