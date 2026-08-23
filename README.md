# DiagnosticExplorer

Add live diagnostic data to a .NET application, then inspect it in a browser.

## Quick Start

Choose one viewer:

| Viewer                                    | Package                         | Browser URL                                                      |
| ----------------------------------------- | ------------------------------- | ---------------------------------------------------------------- |
| A local viewer hosted by your application | `DiagnosticExplorer.SelfHost` | `http://127.0.0.1:1234`                                        |
| A central Diagnostic Explorer service     | `DiagnosticExplorer.Hosting`  | Your DiagnosticService URL, for example`http://localhost:2803` |

Reference the package you chose. Use the current version from NuGet:

```xml
<!-- Local viewer; no separate DiagnosticService required -->
<PackageReference Include="DiagnosticExplorer.SelfHost" Version="5.0.0" />

<!-- Or connect this application to a central DiagnosticService -->
<PackageReference Include="DiagnosticExplorer.Hosting" Version="5.0.0" />
```

Add configuration to `appsettings.json` or `config.json`. Use `SelfHostUrl`
for the local viewer, or `RemoteUrl` for a central service:

```json
{
  "DiagnosticExplorer": {
    "Enabled": true,
    "SelfHostUrl": "http://127.0.0.1:1234",
    "RemoteUrl": "http://localhost:2803/diagnostics"
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

Register the object whose public properties you want to inspect, typically in
its constructor or startup code:

```csharp
using DiagnosticExplorer;

DiagnosticManager.Register(this, "BagName", "CatName");
```

For a local viewer in a console, worker, desktop, or other application without
an ASP.NET Core pipeline, start the viewer with the same configuration:

```csharp
using DiagnosticExplorer.SelfHost;

using DiagnosticSelfHost viewer = await DiagnosticSelfHostingService.StartAsync(
    configuration);
```

Run the application and open `http://127.0.0.1:1234` (or `viewer.Url`) in a
browser. With `DiagnosticExplorer.Hosting`, run `DiagnosticService`, open its
configured URL, and select your registered application.

## Download

[Download the latest x64 MSI](https://github.com/cell001nz/diagnostic-explorer/releases/latest/download/DiagnosticExplorer.Service-win-x64.msi).

## Advanced Hosting

To forward log4net events to DiagnosticExplorer, add the optional integration
package and configure its appenders in `log4net.config`:

```xml
<PackageReference Include="DiagnosticExplorer.Log4Net" Version="5.0.0" />
```

Use the `DiagnosticExplorer.Log4Net` assembly in appender type names, for
example:

```xml
<appender name="Diagnostics" type="DiagnosticExplorer.Log4Net.DiagnosticAppender, DiagnosticExplorer.Log4Net" />
```

For SignalR connections that need custom configuration, pass an
`Action<HttpConnectionOptions>`:

```csharp
services.AddDiagnosticExplorer(
    context.Configuration,
    options => options.AccessTokenProvider = GetCurrentAccessToken);
```

Static start (for non-DI hosts):

```csharp
DiagnosticHostingService.Start(
    "http://diagnostics:2803/diagnostics",
    options => options.AccessTokenProvider = GetCurrentAccessToken);
```

`RemoteUrl` may be a comma-or-semicolon-separated list of hub URLs if you
The `Uri` may be a comma-or-semicolon-separated list of hub URLs if you
want a single application to report to multiple diagnostic servers.
