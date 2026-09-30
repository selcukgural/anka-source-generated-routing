using Anka;
using Anka.Routing;

namespace Demo.Handlers;

[Route("/ping")]
public sealed class PingHandler : IRouteHandler
{
    private static readonly byte[] Body = "pong"u8.ToArray();
    public ValueTask HandleAsync(HttpRequest req, HttpResponseWriter res, CancellationToken ct)
        => res.WriteAsync(200, Body, "text/plain"u8.ToArray(), req.IsKeepAlive, ct);
}

[Route("/health")]
public sealed class HealthHandler : IRouteHandler
{
    private static readonly byte[] Body = "OK"u8.ToArray();
    public ValueTask HandleAsync(HttpRequest req, HttpResponseWriter res, CancellationToken ct)
        => res.WriteAsync(200, Body, "text/plain"u8.ToArray(), req.IsKeepAlive, ct);
}

[Route("/version")]
public sealed class VersionHandler : IRouteHandler
{
    private static readonly byte[] Body = "0.0.1-beta.4"u8.ToArray();
    public ValueTask HandleAsync(HttpRequest req, HttpResponseWriter res, CancellationToken ct)
        => res.WriteAsync(200, Body, "text/plain"u8.ToArray(), req.IsKeepAlive, ct);
}
