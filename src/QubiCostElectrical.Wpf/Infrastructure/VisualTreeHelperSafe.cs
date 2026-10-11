using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Provee utilidades seguras para navegación del árbol visual y lógico en WPF.
/// Evita InvalidOperationException cuando un elemento de inicio o intermedio es un
/// ContentElement o FrameworkContentElement (por ejemplo Run, Bold, Inline, Hyperlink).
/// </summary>
public static class VisualTreeHelperSafe
{
    /// <summary>
    /// Obtiene de forma segura el padre visual o lógico de cualquier DependencyObject.
    /// </summary>
    public static DependencyObject? GetParentSafe(DependencyObject? node)
    {
        if (node == null) return null;

        try
        {
            if (node is Visual or Visual3D)
                return VisualTreeHelper.GetParent(node);
        }
        catch (InvalidOperationException)
        {
            // Escape si WPF no lo reconoce como elemento del árbol visual
        }

        if (node is FrameworkContentElement fce)
            return fce.Parent ?? LogicalTreeHelper.GetParent(node);

        if (node is ContentElement ce)
            return ContentOperations.GetParent(ce) ?? LogicalTreeHelper.GetParent(node);

        return LogicalTreeHelper.GetParent(node);
    }

    /// <summary>
    /// Recorre los ancestros hacia arriba de forma segura hasta encontrar uno del tipo T.
    /// </summary>
    public static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T match)
                return match;

            current = GetParentSafe(current);
        }

        return null;
    }
}
