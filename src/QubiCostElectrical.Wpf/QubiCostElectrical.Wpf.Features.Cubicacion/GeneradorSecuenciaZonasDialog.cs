#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public class GeneradorSecuenciaZonasDialog : Window, IComponentConnector
{
	private bool _initialized;

	internal ComboBox PresetCombo = null!;

	internal TextBox PrefixBox = null!;

	internal ComboBox SeparatorCombo = null!;

	internal TextBox StartBox = null!;

	internal TextBox EndBox = null!;

	internal TextBox StepBox = null!;

	internal TextBox SuffixBox = null!;

	internal CheckBox TwoLevelCheck = null!;

	internal Border TwoLevelPanel = null!;

	internal TextBox Level1Prefix = null!;

	internal TextBox Level1End = null!;

	internal TextBox Level2Prefix = null!;

	internal TextBox Level2End = null!;

	internal ListBox PreviewList = null!;

	private bool _contentLoaded;

	public List<string> ZonasGeneradas { get; private set; } = new List<string>();

	public GeneradorSecuenciaZonasDialog()
	{
		InitializeComponent();
		_initialized = true;
		UpdatePreview();
	}

	private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_initialized || !(PresetCombo?.SelectedItem is ComboBoxItem comboBoxItem))
		{
			return;
		}
		string? text = comboBoxItem.Tag as string;
		bool flag = text == "torre-piso";
		if (TwoLevelCheck != null)
		{
			TwoLevelCheck.IsChecked = flag;
		}
		if (TwoLevelPanel != null)
		{
			TwoLevelPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
		switch (text)
		{
		case "sectores":
			PrefixBox.Text = "S";
			if (SeparatorCombo != null)
			{
				SeparatorCombo.SelectedIndex = 0;
			}
			StartBox.Text = "1";
			EndBox.Text = "30";
			StepBox.Text = "1";
			SuffixBox.Text = "";
			break;
		case "pisos":
			PrefixBox.Text = "P";
			if (SeparatorCombo != null)
			{
				SeparatorCombo.SelectedIndex = 0;
			}
			StartBox.Text = "1";
			EndBox.Text = "20";
			StepBox.Text = "1";
			SuffixBox.Text = "";
			break;
		case "zonas":
			PrefixBox.Text = "Zona";
			if (SeparatorCombo != null)
			{
				SeparatorCombo.SelectedIndex = 3;
			}
			StartBox.Text = "1";
			EndBox.Text = "15";
			StepBox.Text = "1";
			SuffixBox.Text = "";
			break;
		case "niveles":
			PrefixBox.Text = "Nivel";
			if (SeparatorCombo != null)
			{
				SeparatorCombo.SelectedIndex = 3;
			}
			StartBox.Text = "1";
			EndBox.Text = "15";
			StepBox.Text = "1";
			SuffixBox.Text = "";
			break;
		case "locales":
			PrefixBox.Text = "Local";
			if (SeparatorCombo != null)
			{
				SeparatorCombo.SelectedIndex = 3;
			}
			StartBox.Text = "1";
			EndBox.Text = "6";
			StepBox.Text = "1";
			SuffixBox.Text = "";
			break;
		case "areas":
			PrefixBox.Text = "Ãrea";
			if (SeparatorCombo != null)
			{
				SeparatorCombo.SelectedIndex = 3;
			}
			StartBox.Text = "1";
			EndBox.Text = "10";
			StepBox.Text = "1";
			SuffixBox.Text = "";
			break;
		case "torre-piso":
			PrefixBox.Text = "";
			if (SeparatorCombo != null)
			{
				SeparatorCombo.SelectedIndex = 0;
			}
			StartBox.Text = "1";
			EndBox.Text = "1";
			StepBox.Text = "1";
			SuffixBox.Text = "";
			Level1Prefix.Text = "T";
			Level1End.Text = "3";
			Level2Prefix.Text = "P";
			Level2End.Text = "10";
			break;
		}
		UpdatePreview();
	}

	private void OnFieldChanged(object sender, TextChangedEventArgs e)
	{
		if (_initialized)
		{
			UpdatePreview();
		}
	}

	private void OnTwoLevelChanged(object sender, RoutedEventArgs e)
	{
		if (_initialized)
		{
			TwoLevelPanel.Visibility = ((TwoLevelCheck.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
			UpdatePreview();
		}
	}

	private void UpdatePreview()
	{
		ZonasGeneradas = GenerateZones();
		PreviewList.ItemsSource = null;
		PreviewList.ItemsSource = ZonasGeneradas;
	}

	private List<string> GenerateZones()
	{
		List<string> list = new List<string>();
		CheckBox twoLevelCheck = TwoLevelCheck;
		if (twoLevelCheck != null && twoLevelCheck.IsChecked == true)
		{
			if (Level1End == null || Level2End == null)
			{
				return list;
			}
			if (!int.TryParse(Level1End.Text, out var result) || result < 1)
			{
				return list;
			}
			if (!int.TryParse(Level2End.Text, out var result2) || result2 < 1)
			{
				return list;
			}
			string text = Level1Prefix?.Text?.Trim() ?? "";
			string value = Level2Prefix?.Text?.Trim() ?? "";
			string separator = GetSeparator();
			for (int i = 1; i <= result; i++)
			{
				string value2 = text;
				if (text.Length > 0)
				{
					if (char.IsLetter(text[text.Length - 1]))
					{
						char c = text[text.Length - 1];
						if (c >= 'A' && c <= 'Z' && c + i - 1 <= 90)
						{
							string text2 = text;
							value2 = text2.Substring(0, text2.Length - 1) + (char)(c + i - 1);
						}
						else if (c >= 'a' && c <= 'z' && c + i - 1 <= 122)
						{
							string text2 = text;
							value2 = text2.Substring(0, text2.Length - 1) + (char)(c + i - 1);
						}
					}
				}
				for (int j = 1; j <= result2; j++)
				{
					list.Add($"{value2}{separator}{value}{j}");
				}
			}
			return list;
		}
		if (StartBox == null || EndBox == null)
		{
			return list;
		}
		if (!int.TryParse(StartBox.Text, out var result3))
		{
			return list;
		}
		if (!int.TryParse(EndBox.Text, out var result4))
		{
			return list;
		}
		if (!int.TryParse(StepBox?.Text, out var result5) || result5 < 1)
		{
			result5 = 1;
		}
		if (result3 > result4)
		{
			int num = result3;
			result3 = result4;
			result4 = num;
		}
		string text3 = PrefixBox?.Text?.Trim() ?? "";
		string text4 = SuffixBox?.Text?.Trim() ?? "";
		string separator2 = GetSeparator();
		for (int k = result3; k <= result4; k += result5)
		{
			string text5 = text3;
			if (!string.IsNullOrEmpty(separator2))
			{
				text5 += separator2;
			}
			text5 += k;
			if (!string.IsNullOrEmpty(text4))
			{
				text5 += text4;
			}
			list.Add(text5);
		}
		return list;
	}

	private string GetSeparator()
	{
		if (SeparatorCombo?.SelectedItem is ComboBoxItem { Tag: string tag })
		{
			return tag;
		}
		return "";
	}

	private void OnAcceptClick(object sender, RoutedEventArgs e)
	{
		ZonasGeneradas = GenerateZones();
		if (ZonasGeneradas.Count == 0)
		{
			WpfDialogService.Info("Sin zonas", "Revise los valores ingresados. No se generaron zonas.");
		}
		else
		{
			base.DialogResult = true;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacion/generadorsecuenciazonasdialog.xaml", UriKind.Relative);
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
			PresetCombo = (ComboBox)target;
			PresetCombo.SelectionChanged += OnPresetChanged;
			break;
		case 2:
			PrefixBox = (TextBox)target;
			break;
		case 3:
			SeparatorCombo = (ComboBox)target;
			break;
		case 4:
			StartBox = (TextBox)target;
			StartBox.TextChanged += OnFieldChanged;
			break;
		case 5:
			EndBox = (TextBox)target;
			EndBox.TextChanged += OnFieldChanged;
			break;
		case 6:
			StepBox = (TextBox)target;
			StepBox.TextChanged += OnFieldChanged;
			break;
		case 7:
			SuffixBox = (TextBox)target;
			SuffixBox.TextChanged += OnFieldChanged;
			break;
		case 8:
			TwoLevelCheck = (CheckBox)target;
			TwoLevelCheck.Checked += OnTwoLevelChanged;
			TwoLevelCheck.Unchecked += OnTwoLevelChanged;
			break;
		case 9:
			TwoLevelPanel = (Border)target;
			break;
		case 10:
			Level1Prefix = (TextBox)target;
			Level1Prefix.TextChanged += OnFieldChanged;
			break;
		case 11:
			Level1End = (TextBox)target;
			Level1End.TextChanged += OnFieldChanged;
			break;
		case 12:
			Level2Prefix = (TextBox)target;
			Level2Prefix.TextChanged += OnFieldChanged;
			break;
		case 13:
			Level2End = (TextBox)target;
			Level2End.TextChanged += OnFieldChanged;
			break;
		case 14:
			PreviewList = (ListBox)target;
			break;
		case 15:
			((Button)target).Click += OnAcceptClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
