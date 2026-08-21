using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

/// <summary>Editor explícito de la excepción comercial de una partida.</summary>
public sealed class CommercialPartidaRuleDialog : Window
{
    private readonly CommercialPartidaRule rule;
    private readonly CheckBox inherit;
    private readonly ComboBox model;
    private readonly TextBox unitPrice, lumpSum, hours, hourlyRate, materials, expenses, note;

    public CommercialPartidaRuleDialog(ItemizadoRow item, CommercialPartidaRule source)
    {
        rule = source.Clone();
        Title = "Modelo comercial de la partida";
        Width = 510; Height = 590; MinWidth = 460; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;

        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock { Text = "MODELO COMERCIAL DE LA PARTIDA", FontWeight = FontWeights.Bold, FontSize = 16 });
        root.Children.Add(new TextBlock { Text = $"{item.Item} · {item.Descripcion}", Margin = new Thickness(0, 4, 0, 14), TextWrapping = TextWrapping.Wrap });
        inherit = new CheckBox { Content = "Usar el modelo definido por el proyecto", IsChecked = rule.UseProjectModel, Margin = new Thickness(0, 0, 0, 10) };
        root.Children.Add(inherit);
        root.Children.Add(new TextBlock { Text = "MODELO DE EXCEPCIÓN", FontWeight = FontWeights.SemiBold });
        model = new ComboBox { Margin = new Thickness(0, 4, 0, 10), ItemsSource = Enum.GetValues<CommercialCostingModel>(), SelectedItem = rule.Model };
        root.Children.Add(model);
        unitPrice = AddField(root, "Precio unitario CLP", rule.UnitPriceClp);
        lumpSum = AddField(root, "Monto global CLP", rule.LumpSumClp);
        hours = AddField(root, "Horas", rule.LaborHours);
        hourlyRate = AddField(root, "Tarifa hora CLP", rule.LaborHourlyRateClp);
        materials = AddField(root, "Materiales CLP", rule.MaterialsClp);
        expenses = AddField(root, "Gastos reembolsables CLP", rule.ReimbursableExpensesClp);
        root.Children.Add(new TextBlock { Text = "NOTA INTERNA", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 3) });
        note = new TextBox { Text = rule.InternalNote, Height = 44, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        root.Children.Add(note);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Cancelar", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) }; cancel.Click += (_, _) => DialogResult = false;
        var accept = new Button { Content = "Aplicar", MinWidth = 100, IsDefault = true }; accept.Click += Apply;
        buttons.Children.Add(cancel); buttons.Children.Add(accept); root.Children.Add(buttons);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        inherit.Checked += (_, _) => RefreshFields(); inherit.Unchecked += (_, _) => RefreshFields(); model.SelectionChanged += (_, _) => RefreshFields(); RefreshFields();
    }

    public CommercialPartidaRule Result => rule;
    private static TextBox AddField(Panel panel, string label, decimal value)
    { panel.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 3) }); var box = new TextBox { Text = value == 0m ? "" : value.ToString("0.##", CultureInfo.CurrentCulture) }; panel.Children.Add(box); return box; }
    private void RefreshFields()
    { bool active = inherit.IsChecked != true; model.IsEnabled = active; var selected = model.SelectedItem is CommercialCostingModel m ? m : CommercialCostingModel.ApuDirectCostWithCommercialFactors; unitPrice.IsEnabled = active && selected == CommercialCostingModel.UnitPrice; lumpSum.IsEnabled = active && selected == CommercialCostingModel.LumpSum; bool tm = active && selected == CommercialCostingModel.TimeAndMaterials; hours.IsEnabled = tm; hourlyRate.IsEnabled = tm; materials.IsEnabled = tm; expenses.IsEnabled = tm; }
    private void Apply(object? sender, RoutedEventArgs e)
    { rule.UseProjectModel = inherit.IsChecked == true; rule.Model = model.SelectedItem is CommercialCostingModel m ? m : CommercialCostingModel.ApuDirectCostWithCommercialFactors; rule.UnitPriceClp = Parse(unitPrice); rule.LumpSumClp = Parse(lumpSum); rule.LaborHours = Parse(hours); rule.LaborHourlyRateClp = Parse(hourlyRate); rule.MaterialsClp = Parse(materials); rule.ReimbursableExpensesClp = Parse(expenses); rule.InternalNote = note.Text?.Trim() ?? ""; rule.UpdatedAt = AppClock.Now; var errors = rule.Validate(); if (errors.Count > 0) { MessageBox.Show(string.Join(Environment.NewLine, errors), Title, MessageBoxButton.OK, MessageBoxImage.Warning); return; } DialogResult = true; }
    private static decimal Parse(TextBox box) => decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ? value : 0m;
}
