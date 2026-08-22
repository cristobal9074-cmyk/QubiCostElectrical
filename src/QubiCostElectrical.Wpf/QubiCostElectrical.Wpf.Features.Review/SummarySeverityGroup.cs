#nullable enable
using System.Collections.ObjectModel;

namespace QubiCostElectrical.Wpf.Features.Review;

public sealed class SummarySeverityGroup
{
	public string Title { get; init; } = "";

	public int Count { get; init; }

	public ObservableCollection<string> Items { get; init; } = new ObservableCollection<string>();

	public string BgResource { get; init; } = "";

	public string BorderResource { get; init; } = "";

	public string FillResource { get; init; } = "";

	public string TextResource { get; init; } = "";
}
