using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionView : UserControl
{
    private CubicacionViewModel Vm => (CubicacionViewModel)DataContext;

    // Editor de código activo (para navegar autocompletado sin perder foco)
    private TextBox? _codigoEditor;
    // Mientras es false, el mouse quieto sobre la lista no mueve la selección (teclado manda).
    // Se arma al detectar movimiento real del puntero sobre el popup.
    private bool _autocompleteHoverArmed;
    private Point _autocompleteHoverFreezePos;
    private readonly List<DataGridColumn> _sectorColumns = new();
    private bool _suppressSelectionSync;

    // Referencia "pickeada" con ↓/↑ o clic en CantHeaderBox (fórmula de Cantidad oficial, modo
    // Detalle) — -1 significa que no hay ninguna en curso. Mientras se navega con ↓/↑ el token
    // (R1, R2...) se reemplaza en el mismo lugar del texto; al escribir cualquier otra tecla
    // (un operador, un número, borrar...) esa referencia queda "confirmada" en el texto y el
    // siguiente ↓/↑ empieza una referencia nueva — igual que armar una fórmula en una planilla.
    private int _cantPickIndex = -1;
    private int _cantPickTokenStart = -1;
    private int _cantPickTokenLength;

    // Igual que Excel: al entrar a la celda (clic o Tab) queda "seleccionada" — todo el texto
    // resaltado, lista para escribir encima o borrar de un tiro con Supr/Backspace — pero ↓/↑
    // todavía NO arma fórmula (así "solo funciona si entro a editarla"). Recién al escribir el
    // primer carácter se activa el modo edición y ↓/↑ empieza a "pickear" recursos de abajo. Al
    // confirmar con Enter (o al salir de la celda) se apaga de nuevo, para que un ↓/↑ suelto ya
    // no seguiga agregando referencias sobre un valor que el usuario da por terminado.
    private bool _cantEditingStarted;

    private readonly Dictionary<TextBox, bool> _fieldEditingState = new();
}
