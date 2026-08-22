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
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Shell;

public class ShellProjectPickerPopup : UserControl, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty AnchorProperty = DependencyProperty.Register("Anchor", typeof(FrameworkElement), typeof(ShellProjectPickerPopup));

	public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(ShellProjectPickerPopup));

	internal Border ProjectPickerRoot;

	internal Button NewProjectBtn;

	internal Button OpenProjectBtn;

	internal Button CloseProjectBtn;

	private bool _contentLoaded;

	public FrameworkElement? Anchor
	{
		get
		{
			return (FrameworkElement)((DependencyObject)this).GetValue(AnchorProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(AnchorProperty, (object)value);
		}
	}

	public bool IsOpen
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsOpenProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsOpenProperty, (object)value);
		}
	}

	private ShellViewModel? ViewModel => base.DataContext as ShellViewModel;

	public event EventHandler? Closed;

	public event EventHandler? CloseRequested;

	public ShellProjectPickerPopup()
	{
		InitializeComponent();
	}

	private void OnProjectPickerPopupOpened(object? sender, EventArgs e)
	{
		ProjectPickerRoot.Focus();
		Keyboard.Focus(ProjectPickerRoot);
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (NewProjectBtn.IsVisible && NewProjectBtn.IsEnabled)
			{
				NewProjectBtn.Focus();
				Keyboard.Focus(NewProjectBtn);
			}
			else if (OpenProjectBtn.IsVisible && OpenProjectBtn.IsEnabled)
			{
				OpenProjectBtn.Focus();
				Keyboard.Focus(OpenProjectBtn);
			}
		}, (DispatcherPriority)5, Array.Empty<object>());
	}

	private void OnProjectPickerPopupClosed(object? sender, EventArgs e)
	{
		Closed?.Invoke(this, EventArgs.Empty);
	}

	private void OnProjectPickerKeyDown(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			ShellViewModel viewModel = ViewModel;
			if (viewModel != null)
			{
				viewModel.IsProjectPickerOpen = false;
			}
			CloseRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
	}

	private void OnNewProjectClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.NewProjectAsync();
	}

	private void OnOpenProjectFromPickerClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.OpenProjectAsync();
	}

	private void OnCloseProjectFromPickerClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.CloseProjectAsync();
	}

	private void OnSaveAsClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.SaveProjectAsAsync();
	}

	private void OnOpenRecentClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag })
		{
			ViewModel?.OpenRecentProjectAsync(tag);
		}
	}

	private void OnRemoveRecentClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string tag })
		{
			ViewModel?.RemoveRecentProject(tag);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/shell/shellprojectpickerpopup.xaml", UriKind.Relative);
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
			((Popup)target).Opened += OnProjectPickerPopupOpened;
			((Popup)target).Closed += OnProjectPickerPopupClosed;
			break;
		case 2:
			ProjectPickerRoot = (Border)target;
			ProjectPickerRoot.PreviewKeyDown += OnProjectPickerKeyDown;
			break;
		case 3:
			NewProjectBtn = (Button)target;
			NewProjectBtn.Click += OnNewProjectClick;
			break;
		case 4:
			OpenProjectBtn = (Button)target;
			OpenProjectBtn.Click += OnOpenProjectFromPickerClick;
			break;
		case 5:
			((Button)target).Click += OnSaveAsClick;
			break;
		case 6:
			CloseProjectBtn = (Button)target;
			CloseProjectBtn.Click += OnCloseProjectFromPickerClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 7:
			((Button)target).Click += OnOpenRecentClick;
			break;
		case 8:
			((Button)target).Click += OnRemoveRecentClick;
			break;
		}
	}
}
