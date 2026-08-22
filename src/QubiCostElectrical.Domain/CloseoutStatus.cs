#nullable enable
namespace QubiCostElectrical;

public enum CloseoutStatus
{
	Draft,
	InPreparation,
	UnderReview,
	Blocked,
	ReadyForProvisionalReception,
	ApprovedForReception,
	Rejected,
	Cancelled,
	Archived
}
