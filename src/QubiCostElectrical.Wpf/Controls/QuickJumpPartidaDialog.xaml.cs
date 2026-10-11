using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Controls;

public partial class QuickJumpPartidaDialog : Window, INotifyPropertyChanged
{
    private readonly IReadOnlyList<QuickJumpPartidaItem> allItems;
    private QuickJumpPartidaItem? selectedItem;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<QuickJumpPartidaItem> FilteredItems { get; } = new();

    public QuickJumpPartidaItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (ReferenceEquals(selectedItem, value)) return;
            selectedItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
        }
    }

    public string? SelectedPartidaId => SelectedItem?.Id;

    public QuickJumpPartidaDialog(IReadOnlyList<QuickJumpPartidaItem> items, string? initialSelectedId = null)
    {
        InitializeComponent();
        DataContext = this;
        allItems = items ?? Array.Empty<QuickJumpPartidaItem>();

        ApplyFilter(string.Empty);

        if (!string.IsNullOrWhiteSpace(initialSelectedId))
        {
            var match = FilteredItems.FirstOrDefault(x => string.Equals(x.Id, initialSelectedId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedItem = match;
                ResultsList.ScrollIntoView(match);
            }
        }
        else
        {
            SelectFirstValidItem();
        }

        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            if (SelectedItem != null)
                ResultsList.ScrollIntoView(SelectedItem);
        };
    }

    private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string query)
    {
        FilteredItems.Clear();
        var tokens = Tokenize(query);

        foreach (var item in allItems)
        {
            if (Matches(item, tokens))
                FilteredItems.Add(item);
        }

        CountLabel.Text = $"{FilteredItems.Count(x => x.IsSelectable)} elemento(s)";

        if (SelectedItem == null || !FilteredItems.Contains(SelectedItem))
        {
            SelectFirstValidItem();
        }
    }

    private void SelectFirstValidItem()
    {
        SelectedItem = FilteredItems.FirstOrDefault(x => x.IsSelectable);
        if (SelectedItem != null)
            ResultsList.ScrollIntoView(SelectedItem);
    }

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text.Trim()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(t => t.Length > 0)
            .ToArray();
    }

    private static bool Matches(QuickJumpPartidaItem item, string[] tokens)
    {
        if (tokens.Length == 0)
            return true;

        string normCode = Normalize(item.Codigo);
        string normDesc = Normalize(item.Descripcion);
        string normCap = Normalize(item.Capitulo ?? "");
        string normKind = Normalize(item.CategoryBadgeText);

        foreach (var token in tokens)
        {
            bool match = normCode.Contains(token, StringComparison.OrdinalIgnoreCase)
                         || normDesc.Contains(token, StringComparison.OrdinalIgnoreCase)
                         || normCap.Contains(token, StringComparison.OrdinalIgnoreCase)
                         || normKind.Contains(token, StringComparison.OrdinalIgnoreCase);

            if (!match)
                return false;
        }

        return true;
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalizedString = input.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                stringBuilder.Append(c);
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            MoveSelection(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            MoveSelection(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ConfirmSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox)
        {
            ConfirmSelection();
            e.Handled = true;
        }
    }

    private void MoveSelection(int delta)
    {
        if (FilteredItems.Count == 0) return;

        var validItems = FilteredItems.Where(x => x.IsSelectable).ToList();
        if (validItems.Count == 0) return;

        int currentIndex = selectedItem != null ? validItems.IndexOf(selectedItem) : -1;
        int nextIndex = currentIndex + delta;

        if (nextIndex < 0) nextIndex = 0;
        if (nextIndex >= validItems.Count) nextIndex = validItems.Count - 1;

        SelectedItem = validItems[nextIndex];
        ResultsList.ScrollIntoView(SelectedItem);
    }

    private void ConfirmSelection()
    {
        if (SelectedItem == null || !SelectedItem.IsSelectable) return;
        DialogResult = true;
        Close();
    }

    private void OnResultsListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedItem != null && SelectedItem.IsSelectable)
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnResultsListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedItem != null && !SelectedItem.IsSelectable)
        {
            SelectFirstValidItem();
        }
    }
}
