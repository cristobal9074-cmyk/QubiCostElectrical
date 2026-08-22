#nullable enable
namespace QubiCostElectrical.Wpf.Shell;

public sealed class ShortcutItem
{
	public string Text { get; init; }

	public ShortcutItem(string text)
	{
		Text = text;
	}
}
