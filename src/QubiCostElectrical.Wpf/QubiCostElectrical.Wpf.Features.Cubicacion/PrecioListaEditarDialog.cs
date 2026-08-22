#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public class PrecioListaEditarDialog : Window, IComponentConnector
{
	private readonly PrecioListaResumenVm vm;

	internal TextBlock TitleBlock = null!;

	internal TextBox CodigoBox = null!;

	internal TextBox DescripcionBox = null!;

	internal ComboBox UnidadCombo = null!;

	internal TextBox CategoriaBox = null!;

	internal TextBox MatClpBox = null!;

	internal TextBox MoClpBox = null!;

	internal TextBox ProveedorBox = null!;

	internal DatePicker FechaPicker = null!;

	private bool _contentLoaded;

	public PrecioListaEditarDialog(PrecioListaResumenVm vm, PrecioListaMaterialVm? _)
	{
		InitializeComponent();
		this.vm = vm;
		FechaPicker.SelectedDate = DateTime.Today;
	}

	private void OnGuardarClick(object sender, RoutedEventArgs e)
	{
		string text = CodigoBox.Text.Trim();
		string text2 = DescripcionBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			WpfDialogService.Info("Campo requerido", "El cÃ³digo del material es obligatorio.");
			CodigoBox.Focus();
			return;
		}
		if (string.IsNullOrWhiteSpace(text2))
		{
			WpfDialogService.Info("Campo requerido", "La descripciÃ³n del material es obligatoria.");
			DescripcionBox.Focus();
			return;
		}
		if (!decimal.TryParse(MatClpBox.Text, out var result))
		{
			result = 0m;
		}
		if (!decimal.TryParse(MoClpBox.Text, out var result2))
		{
			result2 = 0m;
		}
		string unidad = ((UnidadCombo.SelectedItem is ComboBoxItem comboBoxItem) ? ((comboBoxItem.Content as string) ?? "un") : "un");
		vm.AgregarMaterial(text, text2, unidad, CategoriaBox.Text.Trim(), result, result2, ProveedorBox.Text.Trim(), FechaPicker.SelectedDate ?? DateTime.Today);
		base.DialogResult = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacion/preciolistaeditardialog.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			TitleBlock = (TextBlock)target;
			break;
		case 2:
			CodigoBox = (TextBox)target;
			break;
		case 3:
			DescripcionBox = (TextBox)target;
			break;
		case 4:
			UnidadCombo = (ComboBox)target;
			break;
		case 5:
			CategoriaBox = (TextBox)target;
			break;
		case 6:
			MatClpBox = (TextBox)target;
			break;
		case 7:
			MoClpBox = (TextBox)target;
			break;
		case 8:
			ProveedorBox = (TextBox)target;
			break;
		case 9:
			FechaPicker = (DatePicker)target;
			break;
		case 10:
			((Button)target).Click += OnGuardarClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
