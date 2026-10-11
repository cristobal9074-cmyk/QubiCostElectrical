using System;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class UndoRedoHudCoordinatorTests
{
    [Fact]
    public void NotifyUndo_SetsCorrectPropertiesAndEnablesRevert()
    {
        var coordinator = new UndoRedoHudCoordinator();
        bool reverted = false;

        coordinator.NotifyUndo("Eliminación de partida 1.2", () => reverted = true);

        Assert.True(coordinator.IsVisible);
        Assert.True(coordinator.IsUndo);
        Assert.Equal("Deshecho", coordinator.Title);
        Assert.Equal("↩", coordinator.Glyph);
        Assert.Equal("Eliminación de partida 1.2", coordinator.Description);
        Assert.Equal("Rehacer", coordinator.ActionLabel);
        Assert.Equal("Ctrl+Y", coordinator.ActionHint);
        Assert.True(coordinator.RevertCommand.CanExecute(null));

        coordinator.RevertCommand.Execute(null);

        Assert.True(reverted);
        Assert.False(coordinator.IsVisible);
    }

    [Fact]
    public void NotifyRedo_SetsCorrectPropertiesForRedo()
    {
        var coordinator = new UndoRedoHudCoordinator();
        bool reverted = false;

        coordinator.NotifyRedo("Modificar Cantidad en Partida 1.2", () => reverted = true);

        Assert.True(coordinator.IsVisible);
        Assert.False(coordinator.IsUndo);
        Assert.Equal("Rehecho", coordinator.Title);
        Assert.Equal("↪", coordinator.Glyph);
        Assert.Equal("Modificar Cantidad en Partida 1.2", coordinator.Description);
        Assert.Equal("Deshacer", coordinator.ActionLabel);
        Assert.Equal("Ctrl+Z", coordinator.ActionHint);
        Assert.True(coordinator.RevertCommand.CanExecute(null));

        coordinator.RevertCommand.Execute(null);

        Assert.True(reverted);
        Assert.False(coordinator.IsVisible);
    }

    [Fact]
    public void DismissCommand_HidesHudAndClearsRevert()
    {
        var coordinator = new UndoRedoHudCoordinator();
        bool reverted = false;

        coordinator.NotifyUndo("Eliminar partida 2.1", () => reverted = true);
        Assert.True(coordinator.IsVisible);

        coordinator.DismissCommand.Execute(null);

        Assert.False(coordinator.IsVisible);
        Assert.False(reverted);
        Assert.False(coordinator.RevertCommand.CanExecute(null));
    }

    [Fact]
    public void NotifyUndo_WithEmptyDescription_DefaultsToEdicion()
    {
        var coordinator = new UndoRedoHudCoordinator();

        coordinator.NotifyUndo("");

        Assert.Equal("Edición", coordinator.Description);
        Assert.True(coordinator.IsVisible);
    }
}
