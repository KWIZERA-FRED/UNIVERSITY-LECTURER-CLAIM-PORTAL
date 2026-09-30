namespace Academic_Staff_Engagement_Claim_Processing_System.ViewModels;

public enum StepState
{
	Done,
	Current,
	Waiting,
	Rejected
}

// One node in an approval / signature journey. Shared by every portal.
public sealed record WorkflowStep(
	string Label,
	string ShortLabel,
	StepState State,
	string Tooltip,
	bool IsYou = false);