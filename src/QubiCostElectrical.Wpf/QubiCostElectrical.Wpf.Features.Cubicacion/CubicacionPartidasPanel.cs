// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public class CubicacionPartidasPanel : UserControl, IComponentConnector
{
	internal ListBox PartidasList;

	private bool _contentLoaded;

	public ListBox PartidasListControl => PartidasList;

	public event SelectionChangedEventHandler? PartidaSelectionChanged;

	public CubicacionPartidasPanel()
	{
		InitializeComponent();
	}

	private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		PartidaSelectionChanged?.Invoke(sender, e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacion/cubicacionpartidaspanel.xaml", UriKind.Relative);
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
			PartidasList = (ListBox)target;
			PartidasList.SelectionChanged += OnSelectionChanged;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
