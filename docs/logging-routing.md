# Replayable Logging Stream

`DiagnosticExplorer` keeps one bounded raw logging stream per process for Microsoft.Extensions.Logging, Serilog, NLog, and log4net. A source event has one stream identity (`StreamId`, `Sequence`) even when several configured views display it. The process admits matching events into a five-minute, 5,000-event replay buffer; DiagnosticService maintains one matching relay buffer per process and fans out through small per-browser queues.

Routes admit events process-side and are sent to viewers with each stream initialization. The viewer applies the same ordered rules to project an event into configured categories, overlapping severity views, and resolved drilldown tables. A drilldown route never expands capture beyond the global `ConfigureEventRouting` admission boundary.

All adapters use `EventSinkRouteOptions` from `DiagnosticExplorer.Logging`. The adapters determine a category from their native event as follows:

| Framework                    | Category source                                      |
| ---------------------------- | ---------------------------------------------------- |
| Microsoft.Extensions.Logging | `ILogger` category, including `ILogger<T>`           |
| Serilog                      | `SourceContext`; defaults to `Application`           |
| NLog                         | `LogEventInfo.LoggerName`; defaults to `Application` |

## Route semantics

`CategoryPattern` is case-insensitive. It matches its exact category and child categories separated by `.`, so `Widgets` matches `Widgets` and `Widgets.Rendering`, but not `WidgetShop`. `*` matches every category.

Each matching rule may set `MinLevel` and `MaxLevel`, and can project into multiple destinations. Destinations use the `SinkCategory/SinkName` shorthand; `/` is reserved and cannot appear in either value. The object form with `SinkName` and `SinkCategory` remains available for programmatic configuration. The default `AllMatches` policy preserves overlapping views: a `Widgets` event can appear in both a widget table and a global warning or error table without being copied in the replay buffer. `MostSpecific` selects the longest matching prefix; `FirstMatch` uses declaration order. `StopProcessing` ends rule discovery after the matching rule is included.

Each destination component accepts a `RouteValue`. Strings implicitly become fixed values, while `RouteValue.LoggerSuffix` uses the logger category portion after the matched route prefix. This can provide the sink category:

```csharp
routes.Route(
  typeof(Widget).FullName,
  route => route.To(
    sinkCategory: RouteValue.LoggerSuffix,
    sinkName: "Widget Events"));
```

For example, `WidgetSample.Harness.Widget.Widget X(1)` routes to sink name `Widget Events` and sink category `Widget X(1)`. The direction can be reversed with `route.To("Widgets", RouteValue.LoggerSuffix)`, producing sink category `Widgets` and sink name `Widget X(1)`.

The equivalent destination in configuration is:

```json
{
  "SinkCategory": { "Source": "LoggerSuffix" },
  "SinkName": "Widget Events"
}
```

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
          "Destinations": ["Widgets/Widgets Events"]
        },
        {
          "CategoryPattern": "*",
          "MinLevel": "Warning",
          "Destinations": ["System/Warnings"]
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

Invalid patterns, level ranges, and destinations fail when the adapter is constructed. Route configuration is read at startup; change it by restarting the process.

## Drilldown event views

Use `ConfigureDrillDown` to add repeatable tables for the currently materialized object or collection. Static matchers define a type-wide table; instance matchers resolve a stable logger identity for each displayed object. Collection predicates targeting the same category and table are merged with OR semantics.

```csharp
config.ConfigureDrillDown<Widget>(options =>
  options.Route(
    widget => $"{typeof(Widget).FullName}.{widget.FullName}",
    LoggerNameMatchMode.Exact,
    route => route.To("Widgets", "Widget Events")));
```

The returned drilldown definition only projects events already admitted to the raw stream. Opening or refreshing a drilldown does not add another process subscription or replay buffer.

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

The appender maps `LoggingEvent.LoggerName` and its level to the shared router. Applications can keep ordinary log4net logger levels, filters, SMTP, debug, retro, forwarding, and fallback appenders in `log4net.config`, or construct them in C#. The shared widget workload keeps diagnostic configuration in [WidgetSample.Harness/DiagnosticsConfiguration.cs](../samples/WidgetSample.Harness/DiagnosticsConfiguration.cs), while [WidgetSample.Net48.Log4Net/Program.cs](../samples/WidgetSample.Net48.Log4Net/Program.cs) provides the log4net-specific setup.

## Existing log4net equivalence

The shared harness's `Widgets`, `Gadgets`, and form loggers map to individual category routes. The warning and error destinations are two `*` routes with minimum levels of `Warning` and `Error`. With `AllMatches`, the resulting events fan out across every matching route.
