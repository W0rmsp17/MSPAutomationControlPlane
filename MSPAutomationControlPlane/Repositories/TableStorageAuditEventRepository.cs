using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageAuditEventRepository(TableStorageOptions options) : IAuditEventRepository
{
    private const string PartitionKey = "AUDIT";
    private readonly TableJsonStore<AuditEvent> _store = new(options, "AuditEvents");

    public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        return _store.UpsertAsync(PartitionKey, auditEvent.Id, auditEvent, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken)
    {
        var auditEvents = await _store.ListPartitionAsync(PartitionKey, cancellationToken);
        return auditEvents
            .OrderByDescending(item => item.OccurredAt)
            .ToArray();
    }
}
