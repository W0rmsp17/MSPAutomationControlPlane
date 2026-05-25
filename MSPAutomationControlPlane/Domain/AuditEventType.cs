namespace MSPAutomationControlPlane.Domain;

public enum AuditEventType
{
    ModuleRegistered,
    ClientConnectionRegistered,
    NotificationSubscriptionRegistered,
    NotificationSubscriptionDeleted,
    JobSubmitted,
    JobCompleted
}
