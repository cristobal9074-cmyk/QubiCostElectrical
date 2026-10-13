#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Colección observable de alto rendimiento para WPF. Permite reemplazar colecciones
/// completas emitiendo un único evento <see cref="NotifyCollectionChangedAction.Reset"/>
/// en lugar de N eventos individuales, eliminando la sobrecarga y congelamiento del DataGrid.
/// </summary>
/// <typeparam name="T">Tipo de elemento.</typeparam>
public class FastObservableCollection<T> : ObservableCollection<T>
{
    private bool suppressNotification;

    public FastObservableCollection()
    {
    }

    public FastObservableCollection(IEnumerable<T> collection) : base(collection)
    {
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    /// <summary>
    /// Reemplaza todos los elementos de la colección de manera atómica
    /// emitiendo una única notificación de reset a los controles WPF vinculados.
    /// </summary>
    public void ReplaceRange(IEnumerable<T> collection)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        CheckReentrancy();

        suppressNotification = true;
        try
        {
            Items.Clear();
            foreach (var item in collection)
            {
                Items.Add(item);
            }
        }
        finally
        {
            suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
