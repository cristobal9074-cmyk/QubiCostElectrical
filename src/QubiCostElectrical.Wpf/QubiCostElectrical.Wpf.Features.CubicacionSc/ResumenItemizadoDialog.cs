// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public class ResumenItemizadoDialog : Window, IComponentConnector, IStyleConnector
{
	private readonly ResumenItemizadoViewModel vm;

	internal DataGrid PartidaGrid;

	private bool _contentLoaded;

	public ItemizadoPartidaVm? Result { get; private set; }

	public bool ShouldAddResource { get; private set; }

	public bool ShouldDeleteRows { get; private set; }

	public ResumenItemizadoDialog(IReadOnlyList<ItemizadoPartidaVm> items, string? initialId = null)
	{
		InitializeComponent();
		vm = new ResumenItemizadoViewModel(items, initialId);
		vm.OnCerrar = delegate
		{
			base.DialogResult = false;
		};
		base.DataContext = vm;
		base.Loaded += delegate
		{
			PartidaGrid.Focus();
			if (PartidaGrid.Items.Count > 0 && vm.SelectedRow == null)
			{
				PartidaGrid.SelectedIndex = 0;
			}
		};
	}

	private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Invalid comparison between Unknown and I4
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Invalid comparison between Unknown and I4
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Invalid comparison between Unknown and I4
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Invalid comparison between Unknown and I4
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			base.DialogResult = false;
			e.Handled = true;
		}
		else if ((int)e.Key == 6 && vm.SelectedRow != null && !vm.SelectedRow.EsSeccion)
		{
			NavigateTo(vm.SelectedRow);
			e.Handled = true;
		}
		else
		{
			if (e.IsRepeat)
			{
				return;
			}
			bool num = ((int)Keyboard.Modifiers & 2) == 2;
			bool flag = ((int)Keyboard.Modifiers & 4) == 4;
			if (!num || !flag)
			{
				return;
			}
			Key val = (((int)e.Key == 156) ? e.SystemKey : e.Key);
			if (vm.SelectedRow != null && !vm.SelectedRow.EsSeccion)
			{
				if ((int)val == 44)
				{
					ShouldAddResource = true;
					NavigateTo(vm.SelectedRow);
					e.Handled = true;
				}
				else if ((int)val == 47)
				{
					ShouldDeleteRows = true;
					NavigateTo(vm.SelectedRow);
					e.Handled = true;
				}
			}
		}
	}

	private void OnFilterChipClick(object sender, MouseButtonEventArgs e)
	{
		if (sender is Border { DataContext: FilterChipVm dataContext })
		{
			vm.ActiveFilter = dataContext.Nombre;
		}
	}

	private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (PartidaGrid.SelectedItem is ItemizadoPartidaVm { EsSeccion: false } itemizadoPartidaVm)
		{
			NavigateTo(itemizadoPartidaVm);
		}
	}

	private void NavigateTo(ItemizadoPartidaVm row)
	{
		Result = row;
		base.DialogResult = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/cubicacionsc/resumenitemizadodialog.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((ResumenItemizadoDialog)target).PreviewKeyDown += OnWindowPreviewKeyDown;
			break;
		case 3:
			PartidaGrid = (DataGrid)target;
			PartidaGrid.MouseDoubleClick += OnRowDoubleClick;
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
		if (connectionId == 2)
		{
			((Border)target).MouseLeftButtonDown += OnFilterChipClick;
		}
	}
}
