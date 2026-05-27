using System.Collections.Concurrent;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Repositories;

public sealed class InMemoryDataConsumerConnectorRepository : IDataConsumerConnectorRepository
{
    private readonly ConcurrentDictionary<string, DataConsumerConnector> _connectors = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(DataConsumerConnector connector, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connector.Id))
        {
            throw new ArgumentException("Connector id is required.", nameof(connector));
        }

        _connectors[connector.Id] = connector;
        return Task.CompletedTask;
    }

    public Task<DataConsumerConnector?> GetAsync(string id, CancellationToken cancellationToken)
    {
        _connectors.TryGetValue(id, out var connector);
        return Task.FromResult(connector);
    }

    public Task<IReadOnlyList<DataConsumerConnector>> ListAsync(CancellationToken cancellationToken)
    {
        var connectors = _connectors.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<DataConsumerConnector>>(connectors);
    }
}
