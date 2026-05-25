using MSPAutomationControlPlane.Domain;
using MSPAutomationControlPlane.Repositories;

namespace MSPAutomationControlPlane.Services;

public sealed class AuditService(IAuditEventRepository auditEventRepository)
{
    public Task WriteAsync(
        AuditEventType eventType,
        string actor,
        string summary,
        CancellationToken cancellationToken,
        string? clientConnectionId = null,
        string? moduleId = null,
        string? jobId = null,
        string? resourceId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            Id = $"audit-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
            EventType = eventType,
            OccurredAt = now,
            Actor = actor,
            ClientConnectionId = clientConnectionId,
            ModuleId = moduleId,
            JobId = jobId,
            ResourceId = resourceId,
            Summary = summary
        };

        return auditEventRepository.AddAsync(auditEvent, cancellationToken);
    }

    public Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken)
    {
        return auditEventRepository.ListAsync(cancellationToken);
    }
}
