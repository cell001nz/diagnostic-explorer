# Configuring Diagnostic Explorer in an Application

Use this guide when adding Diagnostic Explorer to an existing .NET process.
The desired result is a browser view that shows a small number of meaningful
application objects, their important state, useful details on demand, and
relevant log events. Do not expose the entire object graph by default.

## Recommended approach

For a .NET generic-host application, reference `DiagnosticExplorer.Hosting`,
register the application's diagnostic source services, and configure Diagnostic
Explorer while building the service collection:

```csharp
using DiagnosticExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<WorkerRegistry>();
builder.Services.ConfigureDiagnosticExplorer(
    builder.Configuration,
    diagnostics =>
    {
        diagnostics.RegisterObjects(RegisterObjects);
        ConfigureDiagnosticTypes(diagnostics);
        ConfigureEventRoutes(diagnostics);
    }
);

await builder.Build().RunAsync();
```

`ConfigureDiagnosticExplorer` registers the configured Remote and SelfHost
listeners as hosted services. Register the application's own services before
calling it, so the registration callback can resolve them.

Diagnostic Explorer hosting is best effort: if a configured diagnostics listener
cannot start, the failure is traced and the application continues without that
listener.

Use the following configuration for the local browser UI. Add a `Remote` host
only when the process should report to a separate DiagnosticService instance.

```json
{
  "DiagnosticExplorer": {
    "Enabled": true,
    "Hosts": [{ "Type": "SelfHost", "Url": "http://127.0.0.1:50101" }],
    "EventRetention": {
      "MaxEventsPerSink": 1000,
      "MaxAgeMinutes": 30
    }
  }
}
```

For an application that does not use a generic host, build a
`DiagnosticConfiguration`, call `DiagnosticManager.Configure(...)`, then start
the configured local listener with `DiagnosticSelfHostingService.StartAsync`.
Keep the returned `DiagnosticSelfHost` alive and call `StopAsync()` during
controlled shutdown. Use the generic-host integration when it is available; it
owns the listener lifecycle for you.

## Register the right objects

Registration determines what the viewer can see. Call `RegisterObjects` once
during startup; Diagnostic Explorer runs the callback each time it collects a
snapshot. Resolve a long-lived service from DI, then register that service and
the current objects it owns:

```csharp
private static void RegisterObjects(IDiagRegistrar registrar)
{
    WorkerRegistry registry = registrar.GetRequiredService<WorkerRegistry>();

    registrar.RegisterService<WorkerRegistry>("Application", "Worker registry");

    foreach (Worker worker in registry.CurrentWorkers)
        registrar.Register(worker, "Workers", worker.Id);
}
```

Use `RegisterService<T>` for a service held by the application service provider.
Use `Register` for a runtime object, including objects created or removed while
the process runs. Do not take a one-time snapshot of dynamic objects at startup:
place the enumeration in the callback so newly created objects appear and
removed objects disappear.

The registration category is the primary UI group or tab. The registration name
is the heading for that object in the group. Both should be stable and useful to
a human. Prefer `worker.Id`, `queue.Name`, or a durable domain key. Avoid list
indexes, timestamps, and randomly generated names because they prevent the UI
from retaining selection and expanded state between refreshes.

Register meaningful roots, not every object in memory. A useful first set is
typically an application service, an active-work collection, a connection pool,
and a current configuration object.

## Configure a focused type profile

Without a profile, public scalar properties are shown automatically. For a
production-friendly display, use `ExcludeAll()` and explicitly add the small
set of properties a support engineer needs first:

```csharp
private static void ConfigureDiagnosticTypes(IDiagConfigurator diagnostics)
{
    diagnostics.Configure<Worker>(options =>
    {
        options.ExcludeAll();

        options.Property(worker => worker.Id).WithLabel("Worker ID");
        options.Property(worker => worker.State).WithCategory("Health");
        options.Property(worker => worker.LastHeartbeat)
            .WithLabel("Last heartbeat")
            .WithCategory("Health")
            .ShowElapsed();
        options.Property(worker => worker.Endpoint)
            .WithCategory("Connection")
            .Expand(initiallyExpanded: false)
            .WithExpandedHover();
    });
}
```

Use `IncludeAll()` when the public-property view is already appropriate, then
use `Exclude(...)` for noisy or unsafe properties. Set
`diagnostics.ApplyAttributes = false` when fluent configuration should be the
only source of display rules; otherwise diagnostic attributes can also
contribute configuration.

For applications with many diagnostic profiles, place each profile in an
`internal static void ConfigureDiagnostics(IDiagConfigurator diagnostics)`
method on its related type, then register its assembly during startup:

```csharp
diagnostics.ConfigureAssemblies(typeof(Worker).Assembly);
```

Diagnostic Explorer invokes compatible methods from the registered assemblies.
In a generic-host application, discovery and configuration are deferred until
diagnostics is first requested. Keep application-wide registration, hosting,
and event-routing configuration in the startup callback.

`WithLabel(...)` changes display text only. `WithCategory(...)` creates a section
inside an object's property bag. Leave properties uncategorized when they are
the immediate summary. Never use a `General` section: null, empty, and
`General` section names intentionally render with no heading.

## Add property statuses

Use `WithStatus(...)` when a property has one or more current states that need an
icon in the viewer. A property can have several active statuses. Statuses use
the fixed `StatusCode` values `Active`, `Inactive`, `Pending`, `Success`,
`Warning`, `Error`, `Alert`, `Danger`, `Running`, `Stopped`, and `Disabled`,
and `Paused`, so the viewer can render a consistent icon for each.
The optional text is available as the icon tooltip and can be generated from
the owning object.

```csharp
options.Property(worker => worker.Endpoint)
    .WithStatus(StatusCode.Active, worker => worker.IsConnected, "Connected")
    .WithStatus(StatusCode.Pending, worker => worker.IsRetrying, worker => $"Retry {worker.RetryCount}");
```

When a property uses `Expand()`, its statuses render beside the generated
category heading rather than in a separate property row.

Use `WithText(...)` to replace the displayed property value while retaining the
underlying value for drilldowns and hover details. An empty string leaves a
status-only row; an owner callback can display a related value instead.

```csharp
options.Property(worker => worker.QueueDepth)
    .WithText("")
    .WithStatus(StatusCode.Running, worker => worker.IsProcessing);

options.Property(worker => worker.QueueDepth)
    .WithText(worker => $"{worker.ActiveJobCount} active jobs");
```

Status icons are small by default. Use `WithIconSize(StatusIconSize.Medium)` or
`WithIconSize(StatusIconSize.Large)` when they should carry more visual weight.

## Choose the right detail technique

Use each technique for its intended scope:

| Need                                                   | Configuration                                      | Result                                                                                    |
| ------------------------------------------------------ | -------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| A small nested object belongs in the main display      | `Expand()`                                         | An expandable property section; first-level sections start expanded unless passed`false`. |
| Details are useful but should not occupy the main view | `WithExpandedHover()` after `Expand()`             | The expanded object is available on hover.                                                |
| A complex object or item needs its own view            | `WithDrillDown()`                                  | The normal rendered value stays visible and opens drilldown.                              |
| The row should be only an action to inspect details    | `WithDrillDownOnly()`                              | The viewer shows`[show more]` rather than a regular value.                                |
| The action needs domain-specific wording               | `WithDrillDownOnly("View connection")`             | The supplied text replaces`[show more]`.                                                  |
| The action text depends on the owning object           | `WithDrillDownOnly(worker => $"View {worker.Id}")` | The callback supplies the text for that object.                                           |
| A calculated or private value is useful                | `Property("Name", instance => value)`              | A named diagnostic-only property.                                                         |
| Derived values need an inline group                    | `Custom(...).Expand()`                             | An expandable main-view section containing the projected members.                         |
| A serialized value is useful occasionally              | `AsJson(...).WithJsonHover()`                      | JSON is fetched only on hover.                                                            |

Add `ConfigureDrillDown<T>(...)` when the drilldown needs a smaller or different
profile than the main view. Otherwise, the normal profile is reused.

```csharp
diagnostics.ConfigureDrillDown<ConnectionInfo>(options =>
{
    options.ExcludeAll();
    options.Property(connection => connection.Endpoint);
    options.Property(connection => connection.IsConnected);
    options.Property(connection => connection.LastError)
        .WithDrillDownOnly("Error details");
});
```

Drilldown is deliberately unavailable for a null object or an empty collection.
Do not work around that rule with placeholder objects; display a concise scalar
state such as `Disconnected` instead.

Use an expanded custom projection when a diagnostic-only group needs multiple
derived members or a collection presentation:

```csharp
options.Custom("Worker inventory", projection =>
{
    projection.Property("Workers", registry => registry.CurrentWorkers)
    .ExpandItems(items => items.WithName(worker => worker.Id));
}).Expand();
```

The outer name becomes the main-view section. Each projected member is rendered
inside it; `ExpandItems(...)` contributes its item sections directly without
adding another collection section.

## Make collection output useful

Collections show a count by default. Pick an output deliberately rather than
serializing a large collection into one value:

```csharp
options.Property(registry => registry.CurrentWorkers)
    .ListItems(items => items
        .WithName(worker => worker.Id)
        .WithValue(worker => worker.State)
        .WithStatus(StatusCode.Running, worker => worker.IsProcessing, "Processing"))
    .WithMaxItems(50)
    .WithDrillDown();

options.Property(registry => registry.EnabledFeatures)
    .ConcatItems(", ")
    .WithTextWrap();
```

Use `ListItems(...)` for a scan-friendly item list. Use
`ExpandItems(items => items.WithName(item => item.Id))` to create an expanded
collection section with a nested section for each item, populated with that
item's diagnostic properties. Without `WithName(...)`, item names default to
the collection name and a zero-based index.
Use `ListItems(items => items.WithStatus(...))` to display one or more status
icons on individual list rows; conditions and tooltip text are evaluated for
each item. Use `ExpandItems(items => items.WithStatus(...))` to show statuses
on individual expanded-item headings, and chain `WithIconSize(...)` to choose
their icon size. A configured name must be distinct for every item; include an
identifier when a readable name alone is not unique. Pass
`initiallyExpanded: false` to start that section collapsed. Use `ConcatItems(...)`
only for short, simple collections.
Chain `WithPrimaryPropertiesOnly()` after `ExpandItems(...)` or `Expand()` to
show only direct, uncategorized properties; nested `Expand()` and `Custom()`
sections are omitted.
`WithTextWrap()` lets concatenated values wrap; it does not change the data
sent by the process. Apply `WithMaxItems(...)` to bound large output. The
viewer makes the omitted item count visible.

## Add diagnostic events

Route logging events to groups users will already inspect. Start with a small
number of event views and a useful minimum level:

```csharp
private static void ConfigureEventRoutes(IDiagConfigurator diagnostics)
{
    diagnostics.ConfigureEventRouting(routes =>
        routes
            .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
            .Route("MyApp.Workers", route => route.AtLeast(LogLevel.Information).To("Workers", "Worker events"))
            .Route("*", route => route.AtLeast(LogLevel.Error).To("System", "Errors"))
    );
}
```

The destination category and name become the event-view location and title in
the viewer. Avoid routing every trace message to every view. Set event retention
limits in configuration so a busy process does not retain unbounded memory.
Use the appropriate logging integration package for the application's logging
stack: `DiagnosticExplorer.Extensions.Logging`, `DiagnosticExplorer.NLog`,
`DiagnosticExplorer.Serilog`, or `DiagnosticExplorer.Log4Net`.

## Verify the end result

After configuration, run the process and open the configured SelfHost URL.
Confirm each of the following before considering the integration complete:

1. The expected registration groups and object names appear.
2. Dynamic objects appear after creation and disappear after removal.
3. The summary properties give useful state without opening details.
4. Expanded sections, hover details, and drilldowns expose deeper state without
   flooding the main display.
5. Editable properties appear only where `AllowSet()` was configured and a
   change has the intended application effect.
6. Event views receive the expected logger categories at the expected levels.
7. Large collections are bounded, and long concatenated text wraps when needed.

If the viewer is empty, check `DiagnosticExplorer:Enabled`, the configured host
URL, that the application services were registered before Diagnostic Explorer,
and that the `RegisterObjects` callback resolves successfully. If dynamic
objects do not change, move the enumeration into `RegisterObjects`. If the
viewer is noisy, reduce the type profile first; do not hide important behavior
behind a broad catch-all category.

For working source examples, see [the widget sample configuration](../samples/WidgetSample.Harness/DiagnosticsConfiguration.cs)
and [the quick-start configuration](../README.md).
