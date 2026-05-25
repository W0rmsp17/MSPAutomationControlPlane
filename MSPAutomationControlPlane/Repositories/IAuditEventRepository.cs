using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken);
}
