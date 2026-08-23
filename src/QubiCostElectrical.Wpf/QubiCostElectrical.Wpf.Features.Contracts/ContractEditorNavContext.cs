using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Contracts;

/// <summary>Null Contract = crear uno nuevo; con valor = editar el contrato existente.</summary>
public sealed record ContractEditorNavContext(Contract? Contract) : WorkspaceNavigationContext;
