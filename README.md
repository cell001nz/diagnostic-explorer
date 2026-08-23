# DiagnosticExplorer

Add live diagnostic data to a .NET application, then inspect it in a browser.

## Dashboard

<img src="Docs/dashboard.png" alt="Diagnostic Explorer dashboard showing registered gadgets and live gadget events" width="760" />

Inspect registered objects, their properties, and live diagnostic events from one browser view.

## Quick Start

Configure one or both hosts:

| Viewer                                    | Package                      | Browser URL                                                     |
| ----------------------------------------- | ---------------------------- | --------------------------------------------------------------- |
| A local viewer hosted by your application | `DiagnosticExplorer.Hosting` | `http://127.0.0.1:50001`                                        |
| A central Diagnostic Explorer service     | `DiagnosticExplorer.Hosting` | Your DiagnosticService URL, for example`http://localhost:50000` |

Reference `DiagnosticExplorer.Hosting`. It starts the host or hosts selected
by configuration:

```xml
<PackageReference Include="DiagnosticExplorer.Hosting" Version="5.0.0" />
```

Add configuration to `appsettings.json` or `config.json`. Each `Hosts` entry
selects a hosting mode. Set either host type or both:

```json
{
  "DiagnosticExplorer": {
    "Enabled": true,
    "Hosts": [
      { "Type": "SelfHost", "Url": "http://127.0.0.1:50001" },
      { "Type": "Remote", "Url": "http://localhost:50000/diagnostics" }
    ]
  }
}
```

For an application using `Microsoft.Extensions.Hosting`, register Diagnostic
Explorer in dependency injection, as in `samples/WidgetSample.Net10.Mel`:

```csharp
using DiagnosticExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDiagnosticExplorer(context.Configuration);
        services.AddTransient<MyApplication>();
    })
    .Build();

host.Start();
```

`host.Start()` starts the configured local viewer and/or remote diagnostic
connection; `host.StopAsync()` stops them. A WinForms application using the
generic host does not need to choose or manage either lifecycle itself.

Register the object whose public properties you want to inspect, typically in
its constructor or startup code:

```csharp
using DiagnosticExplorer;

DiagnosticManager.Register(this, "BagName", "CatName");
```

For an application without a generic host, start the local viewer directly and
retain the returned handle until shutdown:

```csharp
using DiagnosticExplorer;

DiagnosticSelfHost viewer = await DiagnosticSelfHostingService.StartAsync(
    DiagnosticManager.CurrentConfiguration);
```

Run the application and open `http://127.0.0.1:50001` (or `viewer.Url`) in a
browser. With `DiagnosticExplorer.Hosting`, run `DiagnosticService`, open its
configured URL, and select your registered application.

## Download

[Download the latest x64 MSI](https://github.com/cell001nz/diagnostic-explorer/releases/latest/download/DiagnosticExplorer.Service-win-x64.msi).

## Logging and Advanced Hosting

To route `Microsoft.Extensions.Logging` events to DiagnosticExplorer, add the
optional logging provider:

```xml
<PackageReference Include="DiagnosticExplorer.Extensions.Logging" Version="5.0.0" />
```

Configure the Diagnostic Explorer endpoint and routes in the host builder's
`ConfigureServices` callback, then add the MEL provider. Call
`AddDiagnosticExplorer()` after configuring routes; the provider compiles the
current route configuration when it is registered:

```csharp
using DiagnosticExplorer;
using DiagnosticExplorer.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

services.ConfigureDiagnosticExplorer(diagnostics =>
{
    diagnostics.ConfigureHosting(hosting =>
        hosting.AddHost(DiagnosticHostType.Remote, "http://localhost:50000/diagnostics"));
    diagnostics.ConfigureEventRouting(routes =>
        routes.Route("*", route =>
            route.AtLeast(LogLevel.Information)
                .To("System", "Application Events")));
});

services.AddLogging(logging => logging.AddDiagnosticExplorer());

public sealed class MyApplication
{
    private readonly ILogger<MyApplication> logger;

    public MyApplication(ILogger<MyApplication> logger)
    {
        this.logger = logger;
    }

    public void Run()
    {
        logger.LogInformation("Started processing widgets");
    }
}
```

Each `Route` has a category pattern, an optional inclusive level range, and one
or more destinations. `To` takes the destination sink category first and sink
name second. A named category matches that category and its dot-separated
children, while `*` matches every category. The default `AllMatches` mode
writes an event to every matching route, so specific category routes can fan
out alongside a wildcard severity route:

```csharp
routes
    .Route("Widgets", route =>
        route.AtLeast(LogLevel.Information)
            .To("Application", "Widget Events"))
    .Route("*", route =>
        route.AtLeast(LogLevel.Warning)
            .To("System", "Warnings"));
```

Use `routes.UseMatchMode(EventSinkRouteMatchMode.MostSpecific)` to write only
to the longest matching category route, or
`EventSinkRouteMatchMode.FirstMatch` to use the first declared matching route.
`route.StopAfterMatch()` stops evaluating routes that follow it. Duplicate
destinations are written once per event.

For non-DI hosts, start the connection directly:

```csharp
DiagnosticHostingService.Start("http://diagnostics:50000/diagnostics");
```

Use one `Remote` host entry per central diagnostic server.
