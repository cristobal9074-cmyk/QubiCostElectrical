using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Agregar/eliminar/duplicar/mover filas, aplicar materiales y resolución automática desde el
/// Banco de Precios — extraído de CubicacionViewModel.RowMutations.cs (ver plan en
/// ~/.claude/plans/glittery-stargazing-hippo.md). El colaborador más pesado de los 7 (456
/// líneas originales). Comportamiento preservado tal cual.
/// </summary>
internal sealed partial class CubicacionRowMutationService
{
    private readonly CubicacionViewModel parent;
    private readonly BudgetProjectSession session;
    private readonly WpfAppHost host;
    private readonly ICubicacionService cubSvc;
    private readonly CubicacionPresenter cubicacionPresenter;
    private readonly IPriceBankStore priceBankStore;

    public CubicacionRowMutationService(
        CubicacionViewModel parent,
        BudgetProjectSession session,
        WpfAppHost host,
        ICubicacionService cubSvc,
        CubicacionPresenter cubicacionPresenter,
        IPriceBankStore priceBankStore)
    {
        this.parent = parent;
        this.session = session;
        this.host = host;
        this.cubSvc = cubSvc;
        this.cubicacionPresenter = cubicacionPresenter;
        this.priceBankStore = priceBankStore;
    }
}
