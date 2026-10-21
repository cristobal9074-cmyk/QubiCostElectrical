using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Infrastructure;

internal static class SuggestionListKeyboard
{
    public static void Handle(KeyEventArgs e, ListBox list, Popup popup)
    {
        if (e.Key == Key.Escape)
        {
            popup.IsOpen = false;
            e.Handled = true;
            return;
        }

        if (list.Items.Count == 0)
            return;

        if (e.Key == Key.Down)
        {
            Move(list, 1);
            popup.IsOpen = true;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            Move(list, -1);
            popup.IsOpen = true;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || !popup.IsOpen)
            return;

        if (list.SelectedIndex < 0)
            list.SelectedIndex = 0;
        else
        {
            int index = list.SelectedIndex;
            list.SelectedIndex = -1;
            list.SelectedIndex = index;
        }

        e.Handled = true;
    }

    private static void Move(ListBox list, int delta)
    {
        int next = list.SelectedIndex < 0
            ? (delta > 0 ? 0 : list.Items.Count - 1)
            : list.SelectedIndex + delta;
        if (next < 0)
            next = 0;
        if (next >= list.Items.Count)
            next = list.Items.Count - 1;
        list.SelectedIndex = next;
        list.ScrollIntoView(list.SelectedItem);
    }
}
