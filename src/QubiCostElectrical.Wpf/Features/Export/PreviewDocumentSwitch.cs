using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed class PreviewDocumentSwitch : ObservableObject
{
    public const string OfferLetterId = "OfferLetter";
    public const string OfferPdfId = "OfferPdf";

    private string selectedId = OfferLetterId;

    public PreviewDocumentSwitch(Action onChanged)
    {
        SelectCommand = new RelayCommand<string>(id =>
        {
            if (string.IsNullOrWhiteSpace(id) || string.Equals(id, selectedId, StringComparison.Ordinal))
                return;
            SelectedId = id;
            onChanged();
        });
    }

    public string SelectedId
    {
        get => selectedId;
        private set => SetProperty(ref selectedId, value);
    }

    public ICommand SelectCommand { get; }
}
