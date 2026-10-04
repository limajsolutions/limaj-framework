using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Limaj.Framework.Web.Tests.TestSupport;

/// <summary>
/// Executes an <see cref="IResult"/> on a <see cref="DefaultHttpContext"/> backed by a real
/// service provider, so a test sees exactly what the client would receive: status, headers,
/// Content-Type and the serialized body (including whatever IProblemDetailsService adds when
/// the caller registers AddProblemDetails).
/// </summary>
internal static class HttpResultExecutor
{
    public static async Task<ExecutedHttpResult> ExecuteAsync(
        IResult result,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configureServices?.Invoke(services);
        await using var serviceProvider = services.BuildServiceProvider();

        return await ExecuteAsync(result, serviceProvider);
    }

    public static async Task<ExecutedHttpResult> ExecuteAsync(IResult result, IServiceProvider requestServices)
    {
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        var rawBody = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        JsonElement? body = rawBody.Length == 0 ? null : JsonDocument.Parse(rawBody).RootElement.Clone();

        return new ExecutedHttpResult(
            httpContext.Response.StatusCode,
            httpContext.Response.ContentType,
            httpContext.Response.Headers,
            body,
            rawBody);
    }
}

internal sealed record ExecutedHttpResult(
    int StatusCode,
    string? ContentType,
    IHeaderDictionary Headers,
    JsonElement? Body,
    string RawBody)
{
    public string? GetString(string propertyName) =>
        Body is { } body && body.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    public bool HasProperty(string propertyName) =>
        Body is { } body && body.TryGetProperty(propertyName, out _);

    public JsonElement GetProperty(string propertyName) =>
        Body?.GetProperty(propertyName) ?? throw new InvalidOperationException("Response has no body.");
}
