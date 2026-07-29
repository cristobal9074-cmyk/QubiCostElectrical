namespace QubiCostElectrical;

public sealed class SoDPolicyConfig
{
    public bool CreatorCannotApprove { get; set; } = true;
    public bool SubmitterCannotApprove { get; set; } = true;
    public bool LastEditorCannotApprove { get; set; } = true;
    public bool ReviewerCannotEditEconomics { get; set; } = true;
    public bool ApprovalRequiresPriorReview { get; set; } = true;
    public bool DisabledUserCannotAct { get; set; } = true;
    public bool InvalidIntegrityBlocksApproval { get; set; } = true;
    public bool ApprovedOrObsoleteCannotEdit { get; set; } = true;
    public bool InactiveContractBlocksCreation { get; set; } = true;
    public bool PresenterCannotApprove { get; set; } = true;
}
