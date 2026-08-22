# Event Sink Routing

`DiagnosticExplorer` now provides one route model for Microsoft.Extensions.Logging, Serilog, and NLog. A route selects one or more Diagnostic Explorer event sinks from a logging category, which is normally a type-derived namespace.

All adapters use `EventSinkRouteOptions` from `DiagnosticExplorer.Logging`. The adapters determine a category from their native event as follows:

| Framework                    | Category source                                      |
| ---------------------------- | ---------------------------------------------------- |
| Microsoft.Extensions.Logging | `ILogger` category, including `ILogger<T>`           |
| Serilog                      | `SourceContext`; defaults to `Application`           |
| NLog                         | `LogEventInfo.LoggerName`; defaults to `Application` |

## Route semantics

`CategoryPattern` is case-insensitive. It matches its exact category and child categories separated by `.`, so `Widgets` matches `Widgets` and `Widgets.Rendering`, but not `WidgetShop`. `*` matches every category.

Each matching rule may set `MinLevel` and `MaxLevel`, and can write to multiple sinks. The default `AllMatches` policy preserves log4net-style fan-out: a `Widgets` event can go to both a widget sink and a global warning or error sink. `MostSpecific` selects the longest matching prefix; `FirstMatch` uses declaration order. `StopProcessing` ends rule discovery after the matching rule is included.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "DiagnosticExplorer": {
      "MatchMode": "AllMatches",
      "Routes": [
        {
          "CategoryPattern": "Widgets",
          "Destinations": [
            { "SinkName": "Widgets Events", "SinkCategory": "Widgets" }
          ]
        },
        {
          "CategoryPattern": "*",
          "MinLevel": "Warning",
          "Destinations": [{ "SinkName": "Warnings", "SinkCategory": "System" }]
        },
        {
          "CategoryPattern": "*",
          "MinLevel": "Error",
          "Destinations": [{ "SinkName": "Errors", "SinkCategory": "System" }]
        }
      ]
    }
  }
}
```

Invalid patterns, level ranges, and destinations fail when the adapter is constructed. Route configuration is read at startup; change it by restarting the process.

## Microsoft.Extensions.Logging

Reference `DiagnosticExplorer.Extensions.Logging` and configure the provider with the route section:

```csharp
using DiagnosticExplorer.Extensions.Logging;

builder.Logging.AddDiagnosticExplorer(
    builder.Configuration.GetSection("Logging:DiagnosticExplorer"));
```

Keep ordinary `Logging:LogLevel` configuration for normal provider filtering. It runs before Diagnostic Explorer receives the log event.

## Serilog

Reference `DiagnosticExplorer.Serilog`, bind the shared route options, and add one routed sink:

```csharp
using DiagnosticExplorer.Logging;
using DiagnosticExplorer.Serilog;

EventSinkRouteOptions routes = configuration
    .GetSection("Logging:DiagnosticExplorer")
    .Get<EventSinkRouteOptions>();

Log.Logger = new LoggerConfiguration()
    .WriteTo.DiagnosticExplorer(routes)
    .CreateLogger();
```

`SourceContext` is set automatically by the Serilog Microsoft.Extensions.Logging bridge and can be set directly with `ForContext("SourceContext", "Widgets.Component")`. Use Serilog minimum levels and filters for production filtering; the shared router selects Diagnostic Explorer destinations.

## NLog

Reference `DiagnosticExplorer.NLog`, add one target with the shared rules, then apply normal NLog rules:

```csharp
using DiagnosticExplorer.NLog;
using NLog;
using NLog.Config;

LoggingConfiguration nlogConfiguration = new();
nlogConfiguration.AddDiagnosticExplorer("diagnosticExplorer", routes);
nlogConfiguration.AddRuleForAllLevels("diagnosticExplorer");
LogManager.Configuration = nlogConfiguration;
```

The target is registered as `DiagnosticExplorer`, so it can also be included in an NLog configuration. Normal NLog rules remain useful for preliminary logger-name and level filtering; the shared route options determine Diagnostic Explorer sink destinations.

## log4net

Reference `DiagnosticExplorer.Log4Net` and replace per-sink `DiagnosticAppender` declarations with one `RoutingDiagnosticAppender`. It loads the same `DiagnosticExplorer` section shown above from `config.json` by default:

```xml
<root>
  <level value="INFO" />
  <appender-ref ref="DiagnosticExplorerRouter" />
  <appender-ref ref="DebugOutput" />
  <appender-ref ref="DiagnosticRetroAppender" />
</root>

<logger name="Widgets">
  <level value="DEBUG" />
</logger>

<appender name="DiagnosticExplorerRouter"
          type="DiagnosticExplorer.Log4Net.RoutingDiagnosticAppender, DiagnosticExplorer.Log4Net">
  <ConfigurationFile>config.json</ConfigurationFile>
  <ConfigurationSection>DiagnosticExplorer:Routing</ConfigurationSection>
</appender>
```

The appender maps `LoggingEvent.LoggerName` and its level to the shared router. Keep ordinary log4net logger levels, filters, SMTP, debug, retro, forwarding, and fallback appenders in `log4net.config`; only Diagnostic Explorer sink routing moves to JSON. `WidgetSample` keeps its service URL and routes together under `DiagnosticExplorer`, with routes at `DiagnosticExplorer:Routing`; see [WidgetSample/log4net.config](../WidgetSample/log4net.config) and [WidgetSample/config.json](../WidgetSample/config.json).

## WidgetSample Provider Demo

`WidgetSample` uses its internal logging facade to keep the `Gadgets`, `Widgets`, and `WidgetSample.Form1` categories stable while selecting one native backend at startup. The selector is sample-owned; set `WidgetSample:Logging:Provider` in `config.json` to exactly one of:

```json
{
  "WidgetSample": {
    "Logging": {
      "Provider": "Log4Net"
    }
  }
}
```

Supported values are `Log4Net`, `MEL`, `Serilog`, and `NLog`. The selected backend writes directly to its corresponding Diagnostic Explorer integration, and all modes use the same `DiagnosticExplorer:Routing` rules. Restart the sample after changing the value; configuring multiple providers at once is intentionally not supported because it would duplicate events.

## Existing log4net equivalence

The sample's `Widgets`, `Gadgets`, and `WidgetSample.Form1` loggers map to individual `CategoryPattern` routes. The warning and error appenders become two `*` routes with `MinLevel` of `Warning` and `Error`. With `AllMatches`, the resulting events fan out just as they do in [WidgetSample/log4net.config](../WidgetSample/log4net.config).
