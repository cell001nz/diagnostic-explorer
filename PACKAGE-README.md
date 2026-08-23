# Diagnostic Explorer

Diagnostic Explorer exposes live application state, operations, and routed log events to either a central Diagnostic Explorer service or an embedded self-hosted viewer.

## Choose packages

Install `DiagnosticExplorer` to register application objects. Add one hosting package and, optionally, one logging adapter.

| Need                                           | Package                                 |
| ---------------------------------------------- | --------------------------------------- |
| Core object diagnostics                        | `DiagnosticExplorer`                    |
| Central Diagnostic Explorer service connection | `DiagnosticExplorer.Hosting`            |
| In-process browser viewer                      | `DiagnosticExplorer.SelfHost`           |
| log4net routing appender                       | `DiagnosticExplorer.Log4Net`            |
| Microsoft.Extensions.Logging provider          | `DiagnosticExplorer.Extensions.Logging` |
| Serilog sink                                   | `DiagnosticExplorer.Serilog`            |
| NLog target                                    | `DiagnosticExplorer.NLog`               |

Do not add more than one Diagnostic Explorer logging adapter to the same log event path; each adapter writes its own event.

## Default configuration

When adding a Diagnostic Explorer package, add this baseline section to the application's `config.json` or `appsettings.json`. It includes two sample routing entries and is safe to adapt to the application's category names.

```jsonc
{
  "DiagnosticExplorer": {
    "Enabled": true,
    "EventRetention": {
      "MaxEventsPerSink": 1000,
      "MaxAgeMinutes": 30,
    },
    // MinLevel and MaxLevel accept: Trace, Debug, Information, Warning, Error, Critical, None.
    "Routing": {
      "MatchMode": "AllMatches",
      "Routes": [
        {
          "CategoryPattern": "MyCompany.MyApp",
          "MinLevel": "Information",
          "Destinations": ["Application/Application Events"],
        },
        {
          "CategoryPattern": "*",
          "MinLevel": "Warning",
          "Destinations": ["System/Warnings"],
        },
      ],
    },
  },
}
```

`MaxEventsPerSink` is a per-destination event count, not a global memory or byte limit. Events older than `MaxAgeMinutes` are removed during the scheduled purge, which runs every 20 seconds. Add `RemoteUrl` when using `DiagnosticExplorer.Hosting`, or `SelfHostUrl` when using `DiagnosticExplorer.SelfHost`.

The same settings can be specified fluently in code:

```csharp
using DiagnosticExplorer;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Logging;

DiagnosticConfiguration diagnostics = DiagnosticManager.Configure(config =>
  config.Runtime(runtime => runtime
    .Enabled()
    .RemoteUrl("http://localhost:2803/diagnostics")
    .SelfHostUrl("http://localhost:45000")
    .EventRetention(retention => retention
      .WithMaxEventsPerSink(1000)
      .WithMaxAge(TimeSpan.FromMinutes(30)))
    .Routing(routes => routes
      .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
      .Route("MyCompany.MyApp", route => route
        .AtLeast(LogLevel.Information)
        .To("Application", "Application Events"))
      .Route("*", route => route
        .AtLeast(LogLevel.Warning)
        .To("System", "Warnings")))));
```

Pass `diagnostics.RuntimeOptions.Routing` to the selected logging adapter. Remote and self-host startup can consume the same object through `AddDiagnosticExplorer(diagnostics)`, `DiagnosticHostingService.Start(diagnostics)`, `DiagnosticSelfHostingService.StartAsync(diagnostics)`, or `AddDiagnosticSelfHost(diagnostics)`.

## Register diagnostics

Register objects whose public properties and diagnostic attributes should be visible:

```csharp
using DiagnosticExplorer;

DiagnosticManager.Register(orderProcessor, "Order processor", "Orders");
```

### Configure property rendering

Use fluent configuration when the diagnostic type cannot or should not be decorated. Existing attributes remain supported and provide the baseline metadata; fluent calls override only the values they explicitly set.

```csharp
DiagnosticManager.Configure(diagnostics =>
{
  diagnostics.ApplyAttributes = true; // Default; set false for fluent and convention-based rendering only.
  diagnostics.Configure<OrderProcessor>(type =>
  {
    type.OptIn();
    type.Property(processor => processor.Status)
      .Category("Orders")
      .Description("Current processing state");
    type.Collection(processor => processor.PendingOrders)
      .ShowCount()
      .Concatenate(", ")
      .WithMaxItems(10);
  });
});
```

Install the configuration before registering diagnostic objects. `OptIn` includes fluent selections and properties carrying non-ignored diagnostic attributes. `OptOut` includes public properties by default. Explicit `Include` and `Exclude` calls take precedence over attributes and `BrowsableAttribute`.

Set `ApplyAttributes` to `false` to ignore reflected property-rendering metadata, including diagnostic, `Browsable`, `Category`, and `Description` attributes. Fluent rules and built-in rate/date type handling still apply.

Fluent `Property`, `Collection`, `Rate`, `Date`, and `Extended` declarations use `General` when no category is specified. A plain `Include` does not assign a category.

With `DiagnosticExplorer.Hosting`, configure and install the same rules during service registration. Diagnostics then follows the host lifetime:

```csharp
builder.Services
  .ConfigureDiagnosticExplorer(diagnostics =>
    diagnostics.Configure<OrderProcessor>(type =>
    {
      type.OptIn();
      type.Include(processor => processor.Status);
      }));
```

See [Fluent diagnostic configuration](Docs/fluent-configuration.md) for specialized strategies and precedence details.

## Connect to a central service

For an ASP.NET Core application, configure the service endpoint and register the hosting service:

```json
{
  "DiagnosticExplorer": {
    "Enabled": true,
    "RemoteUrl": "http://localhost:2803/diagnostics"
  }
}
```

```csharp
using DiagnosticExplorer;

builder.Services.AddDiagnosticExplorer(builder.Configuration);
```

The application keeps a SignalR connection to the central Diagnostic Explorer service.

## Self-host a viewer

Use `DiagnosticExplorer.SelfHost` when the process should expose its own local viewer:

```csharp
using DiagnosticExplorer.SelfHost;

using DiagnosticSelfHost host = await DiagnosticSelfHostingService.StartAsync(configuration);
```

Open `host.Url` in a browser. Dispose the host or await `StopAsync()` during shutdown.

```json
{
  "DiagnosticExplorer": {
    "Enabled": true,
    "SelfHostUrl": "http://127.0.0.1:1234"
  }
}
```

For an existing ASP.NET Core application:

```csharp
using DiagnosticExplorer.SelfHost;

builder.Services.AddDiagnosticSelfHost(builder.Configuration);

var app = builder.Build();
app.MapDiagnosticSelfHost();
```

## Route log events

All logging adapters use this `DiagnosticExplorer:Routing` section. Categories are case-insensitive namespace prefixes; `*` matches every category. `MinLevel` and `MaxLevel` accept `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, or `None`.

```json
{
  "DiagnosticExplorer": {
    "Routing": {
      "MatchMode": "AllMatches",
      "Routes": [
        {
          "CategoryPattern": "MyCompany.MyApp.Orders",
          "MinLevel": "Information",
          "Destinations": ["Orders/Order Events"]
        },
        {
          "CategoryPattern": "*",
          "MinLevel": "Error",
          "Destinations": ["System/Errors"]
        }
      ]
    }
  }
}
```

`AllMatches` writes to every matching route. Use `MostSpecific` for only the longest matching category, or `FirstMatch` for the first matching route. Destinations use `SinkCategory/SinkName`; `/` is reserved. Restart the application after changing routes.

### Microsoft.Extensions.Logging

```csharp
using DiagnosticExplorer.Extensions.Logging;

builder.Logging.AddDiagnosticExplorer(
    builder.Configuration.GetSection("DiagnosticExplorer:Routing"));
```

The `ILogger` category is routed. Standard MEL filtering runs before this provider.

### Serilog

```csharp
using DiagnosticExplorer.Logging;
using DiagnosticExplorer.Serilog;
using Microsoft.Extensions.Configuration;
using Serilog;

EventSinkRouteOptions routes = configuration
    .GetSection("DiagnosticExplorer:Routing")
    .Get<EventSinkRouteOptions>() ?? new();

Log.Logger = new LoggerConfiguration()
    .WriteTo.DiagnosticExplorer(routes)
    .CreateLogger();
```

Serilog routes by `SourceContext`; set it for direct Serilog loggers when needed.

### NLog

```csharp
using DiagnosticExplorer.Logging;
using DiagnosticExplorer.NLog;
using NLog;
using NLog.Config;

EventSinkRouteOptions routes = configuration
    .GetSection("DiagnosticExplorer:Routing")
    .Get<EventSinkRouteOptions>() ?? new();

LoggingConfiguration nlogConfiguration = new();
nlogConfiguration.AddDiagnosticExplorer("diagnosticExplorer", routes);
nlogConfiguration.AddRuleForAllLevels("diagnosticExplorer");
LogManager.Configuration = nlogConfiguration;
```

NLog routes by logger name. NLog rules decide which events reach the target.

### log4net

```xml
<appender name="DiagnosticExplorerRouter"
          type="DiagnosticExplorer.Log4Net.RoutingDiagnosticAppender, DiagnosticExplorer.Log4Net">
  <ConfigurationFile>config.json</ConfigurationFile>
  <ConfigurationSection>DiagnosticExplorer:Routing</ConfigurationSection>
</appender>

<root>
  <level value="ALL" />
  <appender-ref ref="DiagnosticExplorerRouter" />
</root>
```

log4net routes by logger name. Its standard level filtering occurs before the appender.
