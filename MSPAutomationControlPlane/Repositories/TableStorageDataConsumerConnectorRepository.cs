using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableStorageDataConsumerConnectorRepository(TableStorageOptions options) : IDataConsumerConnectorRepository
{
    private const string PartitionKey = "DATA_CONSUMER";
    private readonly TableJsonStore<DataConsumerConnector> _store = new(options, "DataConsumers");

    public Task AddAsync(DataConsumerConnector connector, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connector.Id))
        {
            throw new ArgumentException("Connector id is required.", nameof(connector));
        }

        return _store.UpsertAsync(PartitionKey, connector.Id, connector, cancellationToken);
    }

    public Task<DataConsumerConnector?> GetAsync(string id, CancellationToken cancellationToken)
    {
        return _store.GetAsync(PartitionKey, id, cancellationToken);
    }

    public async Task<IReadOnlyList<DataConsumerConnector>> ListAsync(CancellationToken cancellationToken)
    {
        var connectors = await _store.ListPartitionAsync(PartitionKey, cancellationToken);
        return connectors
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
