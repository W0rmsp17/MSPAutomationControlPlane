using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;

namespace MSPAutomationControlPlane.Http;

public static class HttpRequestDataExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static async Task<T?> ReadJsonAsync<T>(
        this HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync<T>(
            request.Body,
            JsonOptions,
            cancellationToken);
    }

    public static async Task<HttpResponseData> WriteJsonAsync(
        this HttpRequestData request,
        HttpStatusCode statusCode,
        object value)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(value, JsonOptions));
        return response;
    }

    public static Task<HttpResponseData> WriteProblemAsync(
        this HttpRequestData request,
        HttpStatusCode statusCode,
        string? detail)
    {
        return request.WriteJsonAsync(statusCode, new
        {
            error = detail ?? "The request could not be processed."
        });
    }
}
