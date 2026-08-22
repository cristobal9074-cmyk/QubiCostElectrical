// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public class CubicacionScGridColumns : ResourceDictionary, IComponentConnector, IStyleConnector
{
	private bool _contentLoaded;

	public event RoutedEventHandler? TipoEditorLostFocus;

	public event RoutedEventHandler? FaseEditorLostFocus;

	public event RoutedEventHandler? PisosBtnClick;

	public event EventHandler? PisosPopupOpened;

	public event EventHandler? PisosPopupClosed;

	public event KeyboardFocusChangedEventHandler? PisosFlyoutLostKeyboardFocus;

	public event KeyEventHandler? PisosPopupInputPreviewKeyDown;

	public event RoutedEventHandler? PisosPopupInputGotFocus;

	private void OnTipoEditor_LostFocus(object s, RoutedEventArgs e)
	{
		TipoEditorLostFocus?.Invoke(s, e);
	}

	private void OnFaseEditor_LostFocus(object s, RoutedEventArgs e)
	{
		FaseEditorLostFocus?.Invoke(s, e);
	}

	private void OnPisosBtn_Click(object s, RoutedEventArgs e)
	{
		PisosBtnClick?.Invoke(s, e);
	}

	private void OnPisosPopup_Opened(object s, EventArgs e)
	{
		PisosPopupOpened?.Invoke(s, e);
	}

	private void OnPisosPopup_Closed(object s, EventArgs e)
	{
		PisosPopupClosed?.Invoke(s, e);
	}

	private void OnPisosFlyout_LostKeyboardFocus(object s, KeyboardFocusChangedEventArgs e)
	{
		PisosFlyoutLostKeyboardFocus?.Invoke(s, e);
	}

	private void OnPisosPopupInput_PreviewKeyDown(object s, KeyEventArgs e)
	{
		PisosPopupInputPreviewKeyDown?.Invoke(s, e);
	}

	private void OnPisosPopupInput_GotFocus(object s, RoutedEventArgs e)
	{
		PisosPopupInputGotFocus?.Invoke(s, e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacionsc/cubicacionscgridcolumns.xaml", UriKind.Relative);
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
		switch (connectionId)
		{
		case 1:
			((TextBox)target).LostFocus += OnTipoEditor_LostFocus;
			break;
		case 2:
			((TextBox)target).LostFocus += OnFaseEditor_LostFocus;
			break;
		case 3:
			((Button)target).Click += OnPisosBtn_Click;
			break;
		case 4:
			((Popup)target).Opened += OnPisosPopup_Opened;
			((Popup)target).Closed += OnPisosPopup_Closed;
			break;
		case 5:
			((Border)target).LostKeyboardFocus += OnPisosFlyout_LostKeyboardFocus;
			break;
		case 6:
			((TextBox)target).PreviewKeyDown += OnPisosPopupInput_PreviewKeyDown;
			((TextBox)target).GotFocus += OnPisosPopupInput_GotFocus;
			break;
		case 7:
			((TextBox)target).PreviewKeyDown += OnPisosPopupInput_PreviewKeyDown;
			((TextBox)target).GotFocus += OnPisosPopupInput_GotFocus;
			break;
		}
	}
}
