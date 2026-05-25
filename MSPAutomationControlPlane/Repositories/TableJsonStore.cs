using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;

namespace MSPAutomationControlPlane.Repositories;

public sealed class TableJsonStore<T>(TableStorageOptions options, string tableSuffix)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TableClient _tableClient = new(
        options.ConnectionString,
        $"{options.TablePrefix}{tableSuffix}");

    public async Task UpsertAsync(
        string partitionKey,
        string rowKey,
        T value,
        CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = new TableEntity(partitionKey, rowKey)
        {
            ["Payload"] = JsonSerializer.Serialize(value, JsonOptions)
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<T?> GetAsync(
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                partitionKey,
                rowKey,
                cancellationToken: cancellationToken);

            return Deserialize(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return default;
        }
    }

    public async Task<IReadOnlyList<T>> ListPartitionAsync(
        string partitionKey,
        CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var values = new List<T>();
        var escapedPartitionKey = partitionKey.Replace("'", "''", StringComparison.Ordinal);
        var filter = $"PartitionKey eq '{escapedPartitionKey}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
                           filter,
                           cancellationToken: cancellationToken))
        {
            var value = Deserialize(entity);
            if (value is not null)
            {
                values.Add(value);
            }
        }

        return values;
    }

    public async Task<bool> DeleteAsync(
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        try
        {
            await _tableClient.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static T? Deserialize(TableEntity entity)
    {
        if (!entity.TryGetValue("Payload", out var payload) || payload is not string json)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
