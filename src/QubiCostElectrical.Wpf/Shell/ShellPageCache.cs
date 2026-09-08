using System;
using System.Collections.Generic;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

/// <summary>
/// Caché de páginas del shell por <see cref="ShellRoute"/>.
/// No posee CurrentView ni leave/enter: eso queda en <see cref="ShellViewModel"/>.
/// </summary>
internal sealed class ShellPageCache
{
    private readonly Dictionary<ShellRoute, Entry> entries = new();

    private sealed record Entry(UserControl View, object? ViewModel);

    public bool TryGet(ShellRoute route, out UserControl view, out object? viewModel)
    {
        if (entries.TryGetValue(route, out var entry))
        {
            view = entry.View;
            viewModel = entry.ViewModel;
            return true;
        }

        view = null!;
        viewModel = null;
        return false;
    }

    public void Set(ShellRoute route, UserControl view, object? viewModel)
        => entries[route] = new Entry(view, viewModel);

    /// <summary>
    /// Dispose de ViewModels <see cref="IDisposable"/> y vacía el diccionario.
    /// No anula CurrentView del shell.
    /// </summary>
    public void Clear()
    {
        foreach (var entry in entries.Values)
        {
            if (entry.ViewModel is IDisposable disposable)
                disposable.Dispose();
        }

        entries.Clear();
    }

    /// <summary>Solo pruebas: UserControl vacío sin XAML.</summary>
    public void Seed(ShellRoute route, object? viewModel)
        => Set(route, new UserControl(), viewModel);
}
