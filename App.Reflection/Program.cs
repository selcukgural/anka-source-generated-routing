using System.Diagnostics;
using System.Reflection;
using Anka;
using Anka.Routing;

var sw = Stopwatch.StartNew();

// Classic discovery: scan the assembly, find [Route] classes, create them with Activator.
var routes = new Dictionary<string, IRouteHandler>(StringComparer.Ordinal);
foreach (var type in typeof(Program).Assembly.GetTypes())
{
    var route = type.GetCustomAttribute<RouteAttribute>();
    if (route is null || !typeof(IRouteHandler).IsAssignableFrom(type))
    {
        continue;
    }

    routes[route.Path] = (IRouteHandler)Activator.CreateInstance(type)!;
}

Console.WriteLine($"[routes] discovered={routes.Count} elapsed_us={sw.Elapsed.TotalMicroseconds:F0}");
if (args is ["--probe"])
{
    return;
}

var server = new Server(async (req, res, ct) =>
{
    if (routes.TryGetValue(req.Path, out var handler))
    {
        await handler.HandleAsync(req, res, ct);
        return;
    }

    await res.WriteAsync(404, keepAlive: req.IsKeepAlive, cancellationToken: ct);
}, port: args.Length > 0 ? int.Parse(args[0]) : 8080);

await server.StartAsync();
