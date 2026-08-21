namespace QubiCostElectrical.Wpf.Features.Export;

public sealed class ExportPartidaOptionVm
{
    public ExportPartidaOptionVm(string id, string label, bool isTitle)
    {
        Id = id;
        Label = label;
        IsTitle = isTitle;
    }

    public ExportPartidaOptionVm(QubiCostElectrical.ExportPartidaOption option)
        : this(option.Id, option.Label, option.IsTitle)
    {
    }

    public string Id { get; }
    public string Label { get; }
    public bool IsTitle { get; }
}
