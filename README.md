# DiagnosticExplorer

Add live diagnostic data to a .NET application, then inspect it in a browser.

## Dashboard

[![Diagnostic Explorer dashboard showing registered gadgets and live gadget events](https://github.com/cell001nz/diagnostic-explorer/raw/main/docs/dashboard.png)](https://github.com/cell001nz/diagnostic-explorer/blob/main/docs/dashboard.png)

Inspect registered objects, their properties, and live diagnostic events from one browser view.

## Quick Start

This package adds configuration-based Diagnostic Explorer hosting:

```xml
<PackageReference Include="DiagnosticExplorer.Hosting" Version="5.0.0" />
```

This `appsettings.json` section enables a local viewer and a remote service
connection; use either host or both:

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

This generic-host setup reads the configuration and starts the selected hosts
with the application:

```csharp
using DiagnosticExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(
        (context, services) =>
        {
            services.ConfigureDiagnosticExplorer(diagnostics =>
            {
                diagnostics.ConfigureHosting(context.Configuration);
                ConfigureClasses(diagnostics);
                ConfigureEventRoutes(diagnostics);
            });
            services.AddHostedService<Worker>();
        }
    )
    .Build()
    .RunAsync();
```

This helper chooses which properties to expose:

```csharp
private static void ConfigureClasses(IDiagConfigurator diagnostics)
{
    diagnostics.Configure<Widget>(options =>
    {
        options.OptIn();
        options.Property(widget => widget.Name)
            .Named("Widget name")
            .Category("Details");
    });

    diagnostics.Configure<Gadget>(options =>
    {
        options.OptOut();
        options.Exclude(gadget => gadget.Id);
        options.Property(gadget => gadget.Purpose)
            .Category("Details");
    });
}
```

This helper routes events from named loggers to separate event sinks:

```csharp
private static void ConfigureEventRoutes(IDiagConfigurator diagnostics)
{
    diagnostics.ConfigureEventRouting(routes =>
        routes
            .Route("Widgets", route =>
                route.AtLeast(LogLevel.Information)
                    .To("Widgets", "Widget Events"))
            .Route("Gadgets", route =>
                route.AtLeast(LogLevel.Warning)
                    .To("Gadgets", "Gadget Warnings")));
}
```

This registers an object so its configured properties appear in Diagnostic
Explorer:

```csharp
using DiagnosticExplorer;

DiagnosticManager.Register(this, "BagName", "CatName");
```

This starts and stops the configured hosts in an application without a generic
host:

```csharp
using DiagnosticExplorer;

await DiagnosticHostingService.StartAsync(DiagnosticManager.CurrentConfiguration);
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

The provider sends matching `Microsoft.Extensions.Logging` events to Diagnostic Explorer.

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
