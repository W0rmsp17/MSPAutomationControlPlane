using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class InMemoryAuditEventRepository : IAuditEventRepository
{
    private readonly ConcurrentQueue<AuditEvent> _auditEvents = new();

    public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        _auditEvents.Enqueue(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken)
    {
        var auditEvents = _auditEvents
            .OrderByDescending(item => item.OccurredAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<AuditEvent>>(auditEvents);
    }
}
