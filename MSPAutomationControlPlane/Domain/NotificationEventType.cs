namespace MSPAutomationControlPlane.Domain;

public enum NotificationEventType
{
    JobSucceeded,
    JobFailed,
    ApprovalRequired,
    ModuleRegistered,
    ClientConnectionRegistered,
    ClientConnectionUnhealthy,
    PermissionReadinessFailed,
    RuntimeHealthFailed
}
