namespace MSPAutomationControlPlane.Domain;

public enum JobStatus
{
    Submitted,
    Validated,
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}
