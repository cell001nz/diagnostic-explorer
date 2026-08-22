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

## Register diagnostics

Register objects whose public properties and diagnostic attributes should be visible:

```csharp
using DiagnosticExplorer;

DiagnosticManager.Register(orderProcessor, "Order processor", "Orders");
```

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

All logging adapters use this `DiagnosticExplorer:Routing` section. Categories are case-insensitive namespace prefixes; `*` matches every category.

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
