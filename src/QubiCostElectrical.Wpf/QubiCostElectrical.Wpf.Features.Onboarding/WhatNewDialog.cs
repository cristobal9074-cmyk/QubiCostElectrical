#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Features.Onboarding;

public class WhatNewDialog : Window, IComponentConnector
{
	internal TextBlock ChangelogContent = null!;

	private bool _contentLoaded;

	public WhatNewDialog()
	{
		InitializeComponent();
		LoadChangelog();
	}

	private void LoadChangelog()
	{
		try
		{
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
			if (File.Exists(path))
			{
				string text = File.ReadAllText(path);
				int num = 40;
				string[] array = text.Split('\n');
				if (array.Length > num)
				{
					text = string.Join("\n", array, 0, num) + "\n\n[...]";
				}
				ChangelogContent.Text = text;
			}
			else
			{
				ChangelogContent.Text = "âœ“ Soporte para variable de entorno QCE_SENTRY_DSN\nâœ“ Wizard de bienvenida para nuevos usuarios\nâœ“ DiÃ¡logo de novedades al actualizar versiÃ³n\nâœ“ Mejoras en mensajes de error para usuarios finales\nâœ“ Correcciones de estabilidad y rendimiento";
			}
		}
		catch
		{
			ChangelogContent.Text = "No se pudo cargar el registro de cambios.";
		}
	}

	private void OnCloseClick(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/onboarding/whatnewdialog.xaml", UriKind.Relative);
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
			ChangelogContent = (TextBlock)target;
			break;
		case 2:
			((Button)target).Click += OnCloseClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
