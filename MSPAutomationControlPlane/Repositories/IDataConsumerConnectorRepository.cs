using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public interface IDataConsumerConnectorRepository
{
    Task AddAsync(DataConsumerConnector connector, CancellationToken cancellationToken);

    Task<DataConsumerConnector?> GetAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DataConsumerConnector>> ListAsync(CancellationToken cancellationToken);
}
