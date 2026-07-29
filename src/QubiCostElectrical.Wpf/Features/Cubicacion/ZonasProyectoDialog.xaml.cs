using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class ZonasProyectoDialog : Window
{
    private readonly ObservableCollection<ZonaEditRow> rows = new();

    public ZonasProyectoDialog(IEnumerable<ZonaProyecto> zonas)
    {
        InitializeComponent();

        foreach (var zona in zonas.OrderBy(z => z.Orden))
            rows.Add(new ZonaEditRow(zona.Id, zona.Nombre, zona.Activa));

        rows.CollectionChanged += (_, _) => RefreshNumeros();
        ZonasGrid.ItemsSource = rows;
        RefreshNumeros();
    }

    /// <summary>Zonas resultantes; solo válido cuando DialogResult es true.</summary>
    public List<ZonaProyecto> Zonas { get; } = new();

    private void RefreshNumeros()
    {
        for (int i = 0; i < rows.Count; i++)
            rows[i].Numero = i + 1;

        int activas = rows.Count(r => r.Activa);
        ResumenText.Text = rows.Count == 0
            ? "Sin zonas definidas: se usan sectores genéricos."
            : $"{rows.Count} zonas · {activas} activas";
    }

    private void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        var row = new ZonaEditRow(Guid.NewGuid().ToString("N"), "", true);
        rows.Add(row);
        ZonasGrid.SelectedItem = row;
        ZonasGrid.ScrollIntoView(row);
        ZonasGrid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(row, ZonasGrid.Columns[1]);
        ZonasGrid.BeginEdit();
    }

    private void OnGenerarSecuenciaClick(object sender, RoutedEventArgs e)
    {
        ZonasGrid.CancelEdit(System.Windows.Controls.DataGridEditingUnit.Cell);
        ZonasGrid.CancelEdit(System.Windows.Controls.DataGridEditingUnit.Row);

        var dialog = new GeneradorSecuenciaZonasDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            foreach (var nombre in dialog.ZonasGeneradas)
            {
                if (!rows.Any(r => r.Nombre.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
                    rows.Add(new ZonaEditRow(Guid.NewGuid().ToString("N"), nombre.Trim(), true));
            }
        }
    }

    private void OnAsistenteClick(object sender, RoutedEventArgs e)
    {
        // Cancelar cualquier edición activa o pendiente en el DataGrid para evitar que
        // WPF commitee valores residuales vacíos sobre el primer elemento de la nueva lista.
        ZonasGrid.CancelEdit(System.Windows.Controls.DataGridEditingUnit.Cell);
        ZonasGrid.CancelEdit(System.Windows.Controls.DataGridEditingUnit.Row);

        var assistant = new AsistenteEdificioDialog
        {
            Owner = this
        };

        if (assistant.ShowDialog() == true)
        {
            bool replace = WpfDialogService.Confirm(
                "Estructura de Edificio Generada",
                "¿Desea reemplazar todas las zonas actuales por las nuevas generadas?\n\nSi presiona 'No', las zonas se añadirán al final de la lista actual.");

            if (replace)
            {
                rows.Clear();
            }

            foreach (var nombreZona in assistant.ZonasGeneradas)
            {
                rows.Add(new ZonaEditRow(Guid.NewGuid().ToString("N"), nombreZona, true));
            }
        }
    }

    private void OnQuitarClick(object sender, RoutedEventArgs e)
    {
        if (ZonasGrid.SelectedItem is ZonaEditRow row)
            rows.Remove(row);
    }

    private void OnSubirClick(object sender, RoutedEventArgs e) => MoveSelected(-1);

    private void OnBajarClick(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        if (ZonasGrid.SelectedItem is not ZonaEditRow row)
            return;

        int index = rows.IndexOf(row);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= rows.Count)
            return;

        rows.Move(index, target);
        ZonasGrid.SelectedItem = row;
        ZonasGrid.ScrollIntoView(row);
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        ZonasGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var vacias = rows.Any(r => string.IsNullOrWhiteSpace(r.Nombre));
        if (vacias)
        {
            WpfDialogService.Info("Zonas sin nombre",
                "Hay zonas sin nombre. Complete el nombre o quítelas antes de guardar.");
            return;
        }

        var duplicada = rows
            .GroupBy(r => r.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicada != null)
        {
            WpfDialogService.Info("Zona duplicada",
                $"La zona \"{duplicada.Key}\" está repetida. Cada zona debe tener un nombre único.");
            return;
        }

        Zonas.Clear();
        for (int i = 0; i < rows.Count; i++)
        {
            Zonas.Add(new ZonaProyecto
            {
                Id = rows[i].Id,
                Nombre = rows[i].Nombre.Trim(),
                Orden = i,
                Activa = rows[i].Activa
            });
        }

        DialogResult = true;
    }

    private sealed class ZonaEditRow : INotifyPropertyChanged
    {
        private int numero;
        private string nombre;
        private bool activa;

        public ZonaEditRow(string id, string nombre, bool activa)
        {
            Id = id;
            this.nombre = nombre;
            this.activa = activa;
        }

        public string Id { get; }

        public int Numero
        {
            get => numero;
            set { numero = value; Raise(nameof(Numero)); }
        }

        public string Nombre
        {
            get => nombre;
            set { nombre = value ?? ""; Raise(nameof(Nombre)); }
        }

        public bool Activa
        {
            get => activa;
            set { activa = value; Raise(nameof(Activa)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
