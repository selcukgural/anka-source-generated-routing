namespace Anka.Routing;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RouteAttribute(string path) : Attribute
{
    public string Path { get; } = path;
}

public interface IRouteHandler
{
    ValueTask HandleAsync(HttpRequest request, HttpResponseWriter response, CancellationToken ct);
}
