using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace VubesClub.Verification;

internal sealed record Reply(HttpStatusCode Status, string Body)
{
    public T Read<T>() => JsonSerializer.Deserialize<T>(Body, JsonSerializerOptions.Web)
        ?? throw new InvalidDataException("The endpoint returned an empty JSON document.");
}

// Use the real ASP.NET Core middleware, routing and service scopes without binding a TCP port.
internal sealed class InProcessServer : IServer
{
    private Func<IFeatureCollection, Task>? dispatch;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken token)
        where TContext : notnull
    {
        dispatch = async features =>
        {
            var context = application.CreateContext(features);
            Exception? error = null;
            try
            {
                await application.ProcessRequestAsync(context);
            }
            catch (Exception exception)
            {
                error = exception;
                throw;
            }
            finally
            {
                application.DisposeContext(context, error);
            }
        };
        return Task.CompletedTask;
    }

    public async Task<Reply> Send(string method, string path, object? body = null, string? bearer = null, string? key = null)
    {
        using var input = new MemoryStream(body == null ? [] : JsonSerializer.SerializeToUtf8Bytes(body, JsonSerializerOptions.Web));
        using var output = new MemoryStream();

        var uri = new Uri(new Uri("http://localhost"), path);
        IHeaderDictionary headers = new HeaderDictionary { ["Host"] = "localhost" };

        if (bearer != null) headers.Authorization = "Bearer " + bearer;
        if (key != null) headers["Idempotency-Key"] = key;
        if (body != null)
        {
            headers.ContentType = "application/json";
            headers.ContentLength = input.Length;
        }

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature
        {
            Method = method, Path = uri.AbsolutePath, QueryString = uri.Query, Headers = headers,
            Scheme = uri.Scheme, Protocol = "HTTP/1.1", Body = input
        });
        features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(body != null));
        var response = new HttpResponseFeature();
        features.Set<IHttpResponseFeature>(response);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(output));

        await (dispatch ?? throw new InvalidOperationException("Start the host before sending requests."))(features);
        return new Reply((HttpStatusCode)response.StatusCode, Encoding.UTF8.GetString(output.ToArray()));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose() { }

    private sealed record BodyDetection(bool CanHaveBody) : IHttpRequestBodyDetectionFeature;
}
