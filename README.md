# DiagnosticExplorer

Add live diagnostic data to a .NET application, then inspect it in a browser.

## Dashboard

[![Diagnostic Explorer dashboard showing registered gadgets and live gadget events](docs/dashboard.png)](docs/dashboard.png)

Inspect registered objects, their properties, and live diagnostic events from one browser view.

## Quick Start

This package adds configuration-based Diagnostic Explorer hosting:

```xml
<PackageReference Include="DiagnosticExplorer.Hosting" Version="5.0.0" />
```

This `appsettings.json` section enables a local viewer and a remote-service
connection. Use either host or both, and tune retention without changing code:

```json
{
  "DiagnosticExplorer": {
    "Enabled": true,
    "Hosts": [
      { "Type": "SelfHost", "Url": "http://127.0.0.1:50001" },
      { "Type": "Remote", "Url": "http://localhost:50000/diagnostics" }
    ],
    "EventRetention": {
      "MaxEventsPerSink": 1000,
      "MaxAgeMinutes": 30
    },
    "LogEventRetention": {
      "MaxEvents": 5000,
      "MaxAgeMinutes": 5
    }
  }
}
```

This generic-host setup reads the host configuration, starts the selected
hosts, registers diagnostic objects, and configures their presentation:

```csharp
using DiagnosticExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<WidgetService>();
builder.Services.ConfigureDiagnosticExplorer(
    builder.Configuration,
    diagnostics =>
    {
        diagnostics.RegisterObjects(RegisterObjects);
        ConfigureClasses(diagnostics);
        ConfigureEventRoutes(diagnostics);
    }
);

await builder.Build().RunAsync();
```

`RegisterObjects` runs when diagnostics are collected. Resolve an application
service, then register the dynamic objects it owns:

```csharp
private static void RegisterObjects(IDiagRegistrar registrar)
{
    WidgetService widgetService = registrar.GetRequiredService<WidgetService>();

    registrar.RegisterService<WidgetService>("Application", "Widget service");
    foreach (Widget widget in widgetService.Widgets)
        registrar.Register(widget, "Widgets", widget.Name);
}
```

`RegisterService<T>` resolves `T` from the application service provider before
registering it. Use it for long-lived services, typically singletons.
`Register` remains available for objects created at runtime.

Without a type profile, Diagnostic Explorer displays public scalar properties,
collection counts, and complex values as drilldown icons. Add a profile to
choose exactly what users see and how it behaves:

```csharp
private static void ConfigureClasses(IDiagConfigurator diagnostics)
{
    diagnostics.Configure<Widget>(options =>
    {
        options.ExcludeAll();

        options.Property(widget => widget.Name).Named("Widget name").Category("Details").AllowSet();
        options.Property(widget => widget.Connection).Category("Connection").Expand();
        options.Property(widget => widget.Components)
            .ListItems(list => list.Name(component => component.Name).Value(component => component.Status))
            .WithMaxItems(50);
        options.Property(widget => widget.Configuration).AsJson(100).WithJsonHover().WithDrillDown();
    });

    diagnostics.ConfigureDrillDown<WidgetConfiguration>(options =>
    {
        options.ExcludeAll();
        options.Property(instance => instance.Status);
        options.Property(instance => instance.Owner).AsDrillDown();
    });
}
```

Use `IncludeAll()` to start from every public property, or `ExcludeAll()` to
start from none. `Property` adds display metadata and can render nested object
bags with `Expand()`. For properties declared as an array, `ICollection<T>`,
`IList<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, or `ISet<T>`, use
`Property(...)` to display a count by default, or `ListItems(...)` and
`ConcatItems(...)` to select another rendering. Use `Property(name, value)` for
named or computed properties. `AllowSet()` exposes an editable property.
`AsJson()` with `WithJsonHover()` fetches JSON only on hover.

`AsDrillDown()` makes a complex value, collection item, or custom property
interactive. `AsDrillDownIcon()` provides a compact icon-only entry point. A
dedicated drilldown profile controls the overlay; otherwise the normal type
profile is reused:

```csharp
diagnostics.Configure<Widget>(options =>
{
    options.Property(widget => widget.Configuration).WithDrillDown(maxItems: 50);
    options.Property(widget => widget.Components)
        .ListItems(list => list.Name(component => component.Name))
        .AsDrillDownIcon();
});

diagnostics.ConfigureDrillDown<WidgetConfiguration>(options =>
{
    options.ExcludeAll();
    options.Property(instance => instance.Status);
    options.Property(instance => instance.Owner).WithDrillDown();
});
```

### Private fields and anonymous objects

Use a named delegate property to expose computed or private state. Write the
configuration inside the declaring type when it needs private-field access:

```csharp
private static void ConfigureDiagnostics(IDiagConfigurator diagnostics)
{
    diagnostics.Configure<Widget>(options =>
    {
        options.Property("Retry count", widget => widget._retryCount).Category("Internal");
        options.Property("Last error", widget => widget._lastError?.Message).Category("Internal");
    });
}
```

For `DateTime` and `DateTimeOffset` values, date display options are available
directly from `Property`:

```csharp
options.Property(widget => widget._lastUpdated).ShowElapsed();
options.Property("Last updated", widget => widget._lastUpdated).ShowDate(false).ShowElapsed();
```

For `RateCounter` values, configure rate and total displays the same way:

```csharp
options.Property(widget => widget._requests).ShowRate(false).ShowTotal();
options.Property("Background requests", widget => widget._backgroundRequests).ShowTotal();
```

Return an anonymous object for a small, read-only diagnostic snapshot. Its
generated public properties render in the drilldown view:

```csharp
options.Property(
        "Connection snapshot",
        widget => new
        {
            widget.Connection.Endpoint,
            widget.Connection.IsConnected,
        }
    )
    .AsDrillDownIcon("View connection");
```

See [fluent diagnostic configuration](Docs/fluent-configuration.md) for property, custom-property, category, limit, and nested drilldown behavior.

This helper routes events from named loggers to separate event sinks. Each
integration below uses these same routes:

```csharp
private static void ConfigureEventRoutes(IDiagConfigurator diagnostics)
{
    diagnostics.ConfigureEventRouting(routes =>
        routes
            .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
            .Route("Widgets", route => route.AtLeast(LogLevel.Information).To("Widgets", "Widget Events"))
            .Route("Gadgets", route => route.AtLeast(LogLevel.Warning).To("Gadgets", "Gadget Warnings"))
            .Route("*", route => route.AtLeast(LogLevel.Error).To("System", "Errors")));
}
```

For an application without a generic host, create the configuration, register
objects directly, and start the configured hosts yourself:

```csharp
using DiagnosticExplorer;

DiagnosticConfiguration diagnostics = DiagnosticManager.Configure(config =>
{
    config.ConfigureHosting(applicationConfiguration);
    ConfigureClasses(config);
    ConfigureEventRoutes(config);
});

DiagnosticManager.Register(widget, "Widget 42", "Widgets");

await DiagnosticHostingService.StartAsync(diagnostics);
try
{
    RunApplication();
}
finally
{
    await DiagnosticHostingService.Stop();
}
```

Run the application and open the configured `SelfHost` URL in a browser. For a
`Remote` host, run `DiagnosticService`, open its configured URL, and select
your registered application.

## Download

[Download the latest DiagnosticService Windows Service installer](https://github.com/cell001nz/diagnostic-explorer/releases/latest/download/DiagnosticExplorer.Service-win-x64.msi).

## Microsoft.Extensions.Logging

This optional package forwards `Microsoft.Extensions.Logging` events to
Diagnostic Explorer:

```xml
<PackageReference Include="DiagnosticExplorer.Extensions.Logging" Version="5.0.0" />
```

This registers the logging provider after `ConfigureDiagnosticExplorer`:

```csharp
using DiagnosticExplorer;
using DiagnosticExplorer.Extensions.Logging;
using Microsoft.Extensions.Logging;

services.AddLogging(logging => logging.AddDiagnosticExplorer());
```

The provider sends matching `Microsoft.Extensions.Logging` events to Diagnostic
Explorer, including structured properties:

```csharp
logger.LogInformation("Processed {WidgetCount} widgets", widgetCount);
```

## NLog

This package adds a Diagnostic Explorer target to NLog:

```xml
<PackageReference Include="DiagnosticExplorer.NLog" Version="5.0.0" />
```

This configuration sends NLog events through the routes configured above:

```csharp
using DiagnosticExplorer.NLog;
using NLog;
using NLog.Config;

LoggingConfiguration logging = new();
logging.AddDiagnosticExplorer();
LogManager.Configuration = logging;
```

NLog templates retain their event properties:

```csharp
logger.Info("Processed {WidgetCount} widgets", widgetCount);
```

## Serilog

This package adds a Diagnostic Explorer sink to Serilog:

```xml
<PackageReference Include="DiagnosticExplorer.Serilog" Version="5.0.0" />
```

This configuration sends Serilog events through the routes configured above:

```csharp
using DiagnosticExplorer.Serilog;
using Serilog;

using ILogger logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.DiagnosticExplorer()
    .CreateLogger();
```

Serilog properties are forwarded with the event:

```csharp
logger.Information("Processed {WidgetCount} widgets", widgetCount);
```
