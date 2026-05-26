namespace MSPAutomationControlPlane.Domain;

public enum AuditEventType
{
    ModuleRegistered,
    ClientConnectionRegistered,
    ClientConnectionUpdated,
    NotificationSubscriptionRegistered,
    NotificationSubscriptionDeleted,
    JobSubmitted,
    JobCompleted,
    JobFailed
}
