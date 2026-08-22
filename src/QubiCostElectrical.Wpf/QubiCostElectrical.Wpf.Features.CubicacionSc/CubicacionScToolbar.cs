#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public class CubicacionScToolbar : UserControl, IComponentConnector
{
	internal Button MoreActionsButton = null!;

	private bool _contentLoaded;

	public event RoutedEventHandler? MoreActionsClick;

	public CubicacionScToolbar()
	{
		InitializeComponent();
	}

	private void OnMoreActionsClick(object sender, RoutedEventArgs e)
	{
		MoreActionsClick?.Invoke(sender, e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacionsc/cubicacionsctoolbar.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 1)
		{
			MoreActionsButton = (Button)target;
			MoreActionsButton.Click += OnMoreActionsClick;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
