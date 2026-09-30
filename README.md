# Anka Source-Generated Routing

[![ci](https://github.com/selcukgural/anka-source-generated-routing/actions/workflows/ci.yml/badge.svg)](https://github.com/selcukgural/anka-source-generated-routing/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Attribute routing (`[Route("/ping")]`) for the [Anka](https://github.com/selcukgural/Anka) HTTP server, built
two ways: with runtime **reflection scanning**, and with an **incremental source generator**. Under JIT both work.
Under Native AOT the reflection version silently finds **zero** routes, while the generated version works and
produces a smaller binary.

This is the companion code for the blog post
*"Reflection Taramasından Source Generator'a: Anka'ya AOT Dostu Routing Eklemek"* (Turkish) on
[selcukgural.com](https://selcukgural.com).

> Anka has no built-in routing on purpose. Nothing here changes Anka itself: the router is a separate library
> that sits on top of Anka's single `RequestHandler` delegate.

## Results

Native AOT, osx-arm64, .NET SDK 8.0.203, Anka 0.0.1-beta.6. Discovery time is the median of 7 runs of `--probe`.

| | Reflection scanning | Source generator |
|---|---:|---:|
| Routes found under JIT | 3 | 3 |
| Routes found under Native AOT | **0** | 3 |
| `GET /ping` under Native AOT | 404 | 200 `pong` |
| Trim/AOT warnings at publish | 4 | 0 |
| Binary size | 3.48 MB | 2.70 MB |
| Discovery time, AOT | ~78 µs (finds nothing) | ~6 µs (nothing to discover) |
| Allocation per request for the path | yes (`req.Path` → `string`) | none (`req.PathBytes`) |

The first four rows are the point. The microsecond numbers are noise-level for three handlers.

The blog post was measured with Anka 0.0.1-beta.4 (3.43 MB vs 2.67 MB). Both binaries grew by the same ~30 KB
with the features added to Anka since; the gap between them and every other row are unchanged.

## Layout

| Project | What it is |
|---|---|
| [`Anka.Routing`](Anka.Routing) | `RouteAttribute` and `IRouteHandler`, the user-facing API. References the [Anka NuGet package](https://www.nuget.org/packages/Anka). |
| [`Anka.Routing.Generator`](Anka.Routing.Generator) | `IIncrementalGenerator` that emits a `RouteTable` with a length-then-bytes `switch` over `request.PathBytes`, and reports `ANKA001`–`ANKA003` as build errors. |
| [`App.Reflection`](App.Reflection) | Finds handlers with `Assembly.GetTypes()` + `GetCustomAttribute` + `Activator.CreateInstance`. |
| [`App.Generated`](App.Generated) | Uses the generated `RouteTable`. |
| [`Shared/Handlers.cs`](Shared/Handlers.cs) | The three handlers (`/ping`, `/health`, `/version`), compiled into both apps. |
| [`tools/mapdiff.py`](tools/mapdiff.py) | Compares two Native AOT map files to show where a size difference comes from. |

## Run it

Requires the .NET 8 SDK or later. Native AOT also needs the
[platform prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites)
(Xcode command-line tools on macOS, `clang` + `zlib1g-dev` on Linux).

```bash
# JIT: both discover 3 routes
dotnet run --project App.Reflection -c Release -- --probe
dotnet run --project App.Generated  -c Release -- --probe

# Native AOT: use your RID (osx-arm64, linux-x64, win-x64, ...)
dotnet publish App.Reflection -c Release -r osx-arm64 -o out/reflection   # prints IL2026 / IL2072 / IL2062
dotnet publish App.Generated  -c Release -r osx-arm64 -o out/generated    # no warnings

./out/reflection/App.Reflection --probe   # [routes] discovered=0 ...
./out/generated/App.Generated --probe     # [routes] discovered=3 ...

# Serve (default port 8080)
./out/generated/App.Generated 8080 &
curl localhost:8080/ping                  # pong
```

The generated source is written to `App.Generated/obj/<config>/net8.0/generated/`.

## What the generator emits

```csharp
internal static class RouteTable
{
    private static readonly global::Demo.Handlers.PingHandler s_h0 = new();
    // ...

    public static bool TryDispatch(HttpRequest request, HttpResponseWriter response,
                                   CancellationToken ct, out ValueTask task)
    {
        var path = request.PathBytes;
        switch (path.Length)
        {
            case 5:
                if (path.SequenceEqual("/ping"u8)) { task = s_h0.HandleAsync(request, response, ct); return true; }
                break;
            // ...
        }

        task = default;
        return false;
    }
}
```

This is the code you would write by hand. The generator writes it for you and keeps it in sync with the
`[Route]` attributes.

## Diagnostics

What the reflection version would only find out at runtime, if at all, becomes a build error:

| Id | Condition | Reflection version at runtime |
|---|---|---|
| `ANKA001` | `[Route]` on a class that does not implement `IRouteHandler` | silently skipped |
| `ANKA002` | handler without a public parameterless constructor | `MissingMethodException` |
| `ANKA003` | two handlers declare the same path | last one wins, depending on `GetTypes()` order |

## Where the binary size difference comes from

Measured by publishing variants of the same app that each add one reflection API, then diffing the ILC map files
with `tools/mapdiff.py` (Anka 0.0.1-beta.4). Absolute sizes vary slightly with the toolchain and the Anka
version; the deltas are what matter.

| Step | Δ size |
|---|---:|
| Generated router → explicit `Dictionary<string, IRouteHandler>` + `req.Path` + async lambda (no reflection) | +16.6 KB |
| + `Assembly.GetTypes()` | +72 B |
| + `type.GetCustomAttribute<RouteAttribute>()` | **+763.6 KB** |
| + `IsAssignableFrom` and `Activator.CreateInstance` | ~0 |
| `Activator.CreateInstance(Type)` alone, on known types, no `GetTypes`/attributes | +747 KB |

`GetTypes()` by itself is almost free: it can only return types the compiler kept. The cost comes from the first
API that needs the **reflection execution runtime**, whether that is `GetCustomAttribute` or `Activator`. After
that, the other reflection calls add almost nothing. The ~764 KB breaks down roughly as:

| Component | Share |
|---|---:|
| Reflection metadata and dehydrated tables | 29 % |
| Reflection runtime (`System.Reflection.Runtime`, `Internal.Reflection.*`) | 24 % |
| Runtime type loader (`System.Private.TypeLoader`) | 23 % |
| Public reflection surface (`MethodInfo`, `CustomAttributeData`, `DefaultBinder`, ...) | 9 % |
| Other | 8 % |
| Concurrent collections (reflection caches) | 4 % |
| Number parsing (`System.Number`, used for attribute blobs and binding) | 3 % |

**Pitfall: the cost overlaps with what your app already pulls in.** An earlier version of this demo used
`args.Contains("--probe")`. Compiled as C# 12, that binds to LINQ's `Enumerable.Contains`, which pulls in the
runtime type loader (~315 KB) for *both* apps. That hid part of the reflection cost, and the measured difference
was only ~530 KB. Compiled as C# 14, the same line binds to `MemoryExtensions.Contains(ReadOnlySpan<T>)`, and the
type loader disappears again. The demo now uses `args is ["--probe"]`, which does not depend on the language version.

To reproduce the breakdown:

```bash
dotnet publish App.Reflection -c Release -r osx-arm64 -o out/r -p:IlcGenerateMapFile=true
dotnet publish App.Generated  -c Release -r osx-arm64 -o out/g -p:IlcGenerateMapFile=true
python3 tools/mapdiff.py \
  App.Reflection/obj/Release/net8.0/osx-arm64/native/App.Reflection.map.xml \
  App.Generated/obj/Release/net8.0/osx-arm64/native/App.Generated.map.xml
```

## Limitations

- The generator only sees the current compilation. `[Route]` classes in referenced assemblies are not discovered.
- Handlers need a public parameterless constructor; there is no dependency injection.
- Exact-path matching only: no parameters, no wildcards, no method-based routing.
- The diagnostic location is part of the pipeline model, so edits that shift a handler's position re-run the
  output step. That is fine at this size; a large codebase should move validation into a separate analyzer.

This is a demonstration, not a routing library. Anka itself is in beta and intended for research and
experimentation only.

## License

[MIT](LICENSE)
