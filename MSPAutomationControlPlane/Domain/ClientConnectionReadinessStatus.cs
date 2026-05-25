namespace MSPAutomationControlPlane.Domain;

public enum ClientConnectionReadinessStatus
{
    Unknown = 0,
    Draft = 1,
    PendingConsent = 2,
    Ready = 3,
    Blocked = 4
}
