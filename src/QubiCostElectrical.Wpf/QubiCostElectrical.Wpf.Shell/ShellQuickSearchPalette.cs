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
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Shell;

public class ShellQuickSearchPalette : UserControl, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty AnchorProperty = DependencyProperty.Register("Anchor", typeof(FrameworkElement), typeof(ShellQuickSearchPalette));

	public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(ShellQuickSearchPalette));

	internal ListBox QuickSearchList;

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

	public ShellQuickSearchPalette()
	{
		InitializeComponent();
	}

	private void OnQuickSearchItemClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: ShellViewModel.QuickSearchItemVm dataContext })
		{
			ShellViewModel viewModel = ViewModel;
			if (viewModel != null)
			{
				viewModel.SelectedQuickSearchItem = dataContext;
				viewModel.ExecuteQuickSearchAsync();
			}
		}
	}

	private void OnQuickSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is ListBox listBox)
		{
			ShellViewModel viewModel = ViewModel;
			if (viewModel != null)
			{
				viewModel.SelectedQuickSearchItem = listBox.SelectedItem as ShellViewModel.QuickSearchItemVm;
			}
		}
	}

	private void OnQuickSearchPopupClosed(object? sender, EventArgs e)
	{
		ShellViewModel viewModel = ViewModel;
		if (viewModel != null && viewModel.IsSearchPaletteOpen)
		{
			viewModel.CloseQuickSearch();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/shell/shellquicksearchpalette.xaml", UriKind.Relative);
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
			((Popup)target).Closed += OnQuickSearchPopupClosed;
			break;
		case 2:
			QuickSearchList = (ListBox)target;
			QuickSearchList.SelectionChanged += OnQuickSearchSelectionChanged;
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
		if (connectionId == 3)
		{
			((Button)target).Click += OnQuickSearchItemClick;
		}
	}
}
