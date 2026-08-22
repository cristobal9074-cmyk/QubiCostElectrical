// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Costo;

internal static class CoefFactorKeyboardBehavior
{
	public static readonly DependencyProperty IsEnabledProperty;

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is TextBox textBox)
		{
			if ((bool)args.NewValue)
			{
				textBox.PreviewKeyDown += OnPreviewKeyDown;
				textBox.GotKeyboardFocus += OnGotKeyboardFocus;
			}
			else
			{
				textBox.PreviewKeyDown -= OnPreviewKeyDown;
				textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
			}
		}
	}

	private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
	{
		if (sender is TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
		{
			textBox.SelectAll();
		}
	}

	private static void OnPreviewKeyDown(object sender, KeyEventArgs args)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Invalid comparison between Unknown and I4
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Invalid comparison between Unknown and I4
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Invalid comparison between Unknown and I4
		TextBox textBox = sender as TextBox;
		bool flag = textBox == null;
		if (!flag)
		{
			Key key = args.Key;
			bool flag2 = (((int)key == 6 || (int)key == 24 || (int)key == 26) ? true : false);
			flag = !flag2;
		}
		if (flag)
		{
			return;
		}
		ItemsControl itemsControl = FindAncestor<ItemsControl>((DependencyObject?)(object)textBox);
		if (itemsControl == null || textBox.DataContext == null)
		{
			return;
		}
		int num = itemsControl.Items.IndexOf(textBox.DataContext);
		if (num < 0)
		{
			return;
		}
		int num2 = (((int)args.Key != 24) ? 1 : (-1));
		int num3 = num + num2;
		if ((int)args.Key == 6 && (int)Keyboard.Modifiers == 4)
		{
			num3 = num - 1;
		}
		if (num3 < 0 || num3 >= itemsControl.Items.Count)
		{
			return;
		}
		DependencyObject val = itemsControl.ItemContainerGenerator.ContainerFromIndex(num3);
		if (val != null)
		{
			TextBox textBox2 = FindTextBox(val, textBox);
			if (textBox2 != null)
			{
				args.Handled = true;
				textBox2.Focus();
				textBox2.SelectAll();
			}
		}
	}

	private static TextBox? FindTextBox(DependencyObject root, TextBox source)
	{
		List<TextBox> source2 = FindDescendants<TextBox>(root);
		Grid grid = FindAncestor<Grid>((DependencyObject?)(object)source);
		int index = ((grid != null) ? FindDescendants<TextBox>((DependencyObject)(object)grid).IndexOf(source) : 0);
		return source2.ElementAtOrDefault(index);
	}

	private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
	{
		while (element != null)
		{
			T val = (T)(object)((element is T) ? element : null);
			if (val != null)
			{
				return val;
			}
			element = VisualTreeHelper.GetParent(element);
		}
		return default(T);
	}

	private static List<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
	{
		List<T> list = new List<T>();
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null)
			{
				list.Add(val);
			}
			list.AddRange(FindDescendants<T>(child));
		}
		return list;
	}

	static CoefFactorKeyboardBehavior()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(CoefFactorKeyboardBehavior), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
	}
}
