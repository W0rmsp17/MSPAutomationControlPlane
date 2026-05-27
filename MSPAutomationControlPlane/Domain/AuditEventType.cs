namespace MSPAutomationControlPlane.Domain;

public enum AuditEventType
{
    ModuleRegistered,
    ClientConnectionRegistered,
    ClientConnectionUpdated,
    NotificationSubscriptionRegistered,
    NotificationSubscriptionDeleted,
    DataConsumerConnectorRegistered,
    DerivedArtifactCreated,
    JobSubmitted,
    JobCompleted,
    JobFailed
}
