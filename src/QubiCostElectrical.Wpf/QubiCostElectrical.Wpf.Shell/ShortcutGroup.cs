#nullable enable
using System.Collections.ObjectModel;

namespace QubiCostElectrical.Wpf.Shell;

public sealed class ShortcutGroup
{
	public string Name { get; init; }

	public ObservableCollection<ShortcutItem> Shortcuts { get; } = new ObservableCollection<ShortcutItem>();

	public ShortcutGroup(string name, params ShortcutItem[] shortcuts)
	{
		Name = name;
		foreach (ShortcutItem item in shortcuts)
		{
			Shortcuts.Add(item);
		}
	}
}
