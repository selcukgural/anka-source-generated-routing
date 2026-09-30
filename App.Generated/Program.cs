using System.Diagnostics;
using Anka;
using Anka.Routing.Generated;

var sw = Stopwatch.StartNew();
Console.WriteLine($"[routes] discovered={RouteTable.Count} elapsed_us={sw.Elapsed.TotalMicroseconds:F0}");
if (args is ["--probe"])
{
    return;
}

var server = new Server((req, res, ct) =>
    RouteTable.TryDispatch(req, res, ct, out var task)
        ? task
        : res.WriteAsync(404, keepAlive: req.IsKeepAlive, cancellationToken: ct),
    port: args.Length > 0 ? int.Parse(args[0]) : 8080);

await server.StartAsync();
