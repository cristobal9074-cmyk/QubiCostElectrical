#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public class SolicitudHojaPreviewControl : UserControl, IComponentConnector
{
	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(SolicitudHojaPreviewControl));

	public static readonly DependencyProperty DescripcionHeaderProperty = DependencyProperty.Register("DescripcionHeader", typeof(string), typeof(SolicitudHojaPreviewControl));

	internal SolicitudHojaPreviewControl root = null!;

	private bool _contentLoaded;

	public IEnumerable ItemsSource
	{
		get
		{
			return (IEnumerable)((DependencyObject)this).GetValue(ItemsSourceProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ItemsSourceProperty, (object)value);
		}
	}

	public string DescripcionHeader
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(DescripcionHeaderProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(DescripcionHeaderProperty, (object)value);
		}
	}

	public SolicitudHojaPreviewControl()
	{
		InitializeComponent();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/solicitudes/solicitudhojapreviewcontrol.xaml", UriKind.Relative);
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
			root = (SolicitudHojaPreviewControl)target;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
