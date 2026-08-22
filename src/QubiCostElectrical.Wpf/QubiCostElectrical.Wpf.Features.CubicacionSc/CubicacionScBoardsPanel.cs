#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public class CubicacionScBoardsPanel : UserControl, IComponentConnector, IStyleConnector
{
	public delegate void VisibleChangedHandler(object sender, DependencyPropertyChangedEventArgs e);

	private bool _contentLoaded;

	public event MouseButtonEventHandler? TableroDoubleClick;

	public event RoutedEventHandler? TableroEditClick;

	public event VisibleChangedHandler? NombreVisibleChanged;

	public event RoutedEventHandler? NombreLostFocus;

	public event KeyEventHandler? NombreKeyDown;

	public CubicacionScBoardsPanel()
	{
		InitializeComponent();
	}

	private void OnTableroDoubleClick(object sender, MouseButtonEventArgs e)
	{
		TableroDoubleClick?.Invoke(sender, e);
	}

	private void OnTableroEditClick(object sender, RoutedEventArgs e)
	{
		TableroEditClick?.Invoke(sender, e);
	}

	private void OnNombreVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		NombreVisibleChanged?.Invoke(sender, e);
	}

	private void OnNombreLostFocus(object sender, RoutedEventArgs e)
	{
		NombreLostFocus?.Invoke(sender, e);
	}

	private void OnNombreKeyDown(object sender, KeyEventArgs e)
	{
		NombreKeyDown?.Invoke(sender, e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacionsc/cubicacionscboardspanel.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		_contentLoaded = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		switch (connectionId)
		{
		case 1:
		{
			EventSetter eventSetter = new EventSetter();
			eventSetter.Event = Control.MouseDoubleClickEvent;
			eventSetter.Handler = new MouseButtonEventHandler(OnTableroDoubleClick);
			((Style)target).Setters.Add(eventSetter);
			break;
		}
		case 2:
			((TextBox)target).IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnNombreVisibleChanged);
			((TextBox)target).LostFocus += OnNombreLostFocus;
			((TextBox)target).KeyDown += OnNombreKeyDown;
			break;
		case 3:
			((Button)target).Click += OnTableroEditClick;
			break;
		}
	}
}
