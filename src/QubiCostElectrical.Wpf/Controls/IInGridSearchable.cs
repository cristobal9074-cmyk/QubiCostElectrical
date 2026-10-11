namespace QubiCostElectrical.Wpf.Controls;

/// <summary>
/// Contrato unificado para vistas con grillas que soportan búsqueda in-grid (Ctrl+F / F3).
/// Permite al ShellWindow redirigir atajos globales de teclado a la vista activa
/// sin importar dónde esté el foco de teclado en ese momento.
/// </summary>
public interface IInGridSearchable
{
    void OpenGridSearch();
    void StepGridSearch(bool backward);
    void CloseGridSearch();
    bool IsGridSearchOpen { get; }
}
