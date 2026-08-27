# Fluent diagnostic configuration

Fluent configuration controls how registered object properties become diagnostic `PropertyBag` values without requiring changes to the registered classes. It overlays, rather than replaces, `PropertyAttribute`, `CollectionPropertyAttribute`, `RatePropertyAttribute`, `DatePropertyAttribute`, and `ExtendedPropertyAttribute`.

## Installation

For console, desktop, and self-hosted applications, build and install one configuration before registering diagnostic objects:

```csharp
DiagnosticManager.Configure(configuration =>
{
    configuration.ApplyAttributes = true;
    configuration.Configure<MyService>(type =>
    {
        type.ExcludeAll();
        type.Include(service => service.Status);
    });
});
```

For applications using `DiagnosticExplorer.Hosting`, configure the snapshot through the service collection. This registers the hosted service, which starts and stops diagnostics with the host lifetime:

```csharp
builder.Services
    .ConfigureDiagnosticExplorer(diagnostics =>
        diagnostics.Configure<MyService>(type =>
        {
            type.ExcludeAll();
            type.Include(service => service.Status);
        }));
```

Installing another configuration replaces the complete global snapshot and invalidates cached property getters. It does not merge with the previously installed configuration. The installed snapshot is immutable, so changing the original builder afterward has no effect until it is installed again.

## Attribute handling

`ApplyAttributes` defaults to `true`, preserving the existing attribute-based behavior and using fluent rules as overrides. Set it to `false` when rendering should use only fluent configuration and built-in type conventions:

```csharp
DiagnosticConfiguration configuration = new()
{
    ApplyAttributes = false
};
```

When disabled, property rendering ignores `DiagnosticClassAttribute`, all diagnostic property attributes, and component-model `Browsable`, `Category`, and `Description` metadata. Fluent inclusion, display metadata, collection/rate/date/extended strategies, and built-in rate/date type handling continue to apply. Operation attributes are outside this setting and continue to control exposed methods.

## Inclusion

`ExcludeAll()` excludes ordinary properties unless selected with `Include`, `Property`, `Collection`, `Rate`, `Date`, or `Extended`. Properties with a non-ignored diagnostic property attribute remain included.

`IncludeAll()` includes public properties by default. Existing `Browsable(false)` and ignored diagnostic properties remain excluded unless explicitly included.

The inclusion order is:

1. Explicit fluent `Include` or `Exclude`.
2. A diagnostic property attribute, including its `Ignore` value.
3. `BrowsableAttribute`.
4. Fluent `ExcludeAll` or `IncludeAll`.
5. Existing `DiagnosticClassAttribute.AttributedPropertiesOnly` behavior.

`EventSink` properties remain hidden regardless of configuration.

Configuration for a base class applies to derived runtime types. Rules configured for the derived class override matching base rules.

## Shared property metadata

`Property` selects a property and can override shared display metadata:

```csharp
configuration.Configure<MyService>(type =>
{
    type.Property(service => service.Status)
        .Named("Current status")
        .Category("Service")
        .Description("Current processing state")
        .Format("{0:N2}")
        .AllowSet();
});
```

Only called methods override attribute values. For example, setting `Category` preserves an attributed name, description, format, and settable state.

Every property without an explicit category uses `General`, including properties selected with `Include` and properties discovered by opt-out or convention-based rendering.

## Collections

A collection can emit one or more outputs in call order:

```csharp
configuration.Configure<MyService>(type =>
{
    type.Collection(service => service.WorkItems)
        .ShowCount()
        .Concatenate(Environment.NewLine)
        .WithMaxItems(10);
});
```

When count and another mode are combined, the default count name is `{PropertyName} count`; the value-producing mode retains the configured or source property name. Each mode also accepts an explicit output name.

Typed selectors configure list and category rendering without string property names:

```csharp
type.Collection(service => service.WorkItems)
    .List(
        name: item => item.Name,
        value: item => item.Status,
        description: item => item.Description,
        category: item => item.Group)
    .WithMaxItems(25);

type.Collection(service => service.WorkItems)
    .Categories(item => item.Name)
    .WithMaxItems(25);
```

Use a name and value delegate when the collection is computed or backed by a non-public member:

```csharp
type.Collection("Queued work", service => service.GetQueuedWork())
    .List(items => items.Name(item => item.Name).Value(item => item.Status));
```

`WithMaxItems` limits concatenated, list, and category output. Count always reports the full collection size.

`Extended` also accepts a name and value delegate for computed or non-public nested objects:

```csharp
type.Extended("Connection", service => service.GetConnectionDetails());
```

## Drilldown

`WithDrillDown()` makes a rendered property name and value open the underlying object in a diagnostic dialog. It applies to configured properties, named delegate properties, collection list items, and collection categories:

```csharp
configuration.DrillDownMaxItems = 100;

configuration.Configure<MyService>(type =>
{
    type.Property(service => service.Connection)
        .WithDrillDown();

    type.Property(service => service.Configuration)
        .Named("View configuration")
        .AsDrillDownIcon();

    type.Property("Pending items", service => service.PendingItems)
        .WithDrillDown(maxItems: 25);

    type.Collection(service => service.WorkItems)
        .List(items => items.Name(item => item.Name).Value(item => item.Status))
        .Categories(item => item.Group)
        .WithDrillDown();
});
```

`WithDrillDown()` renders the property name and value as links. `AsDrillDownIcon()` renders the property name as ordinary text, suppresses its display value, and places a drilldown icon beside the name. Both methods accept an optional `maxItems` argument for enumerable targets.

Collection list links target the collection item, even when `Value(...)` displays another value. Category-mode collections show a drilldown link beside each generated subbag name. Complex property and named delegate-property values target their returned object. Null and scalar values do not produce drilldown links.

Enumerable targets render as element bags named `[0]`, `[1]`, and so on. `DrillDownMaxItems` is the global enumerable limit and defaults to 100; the optional `maxItems` argument overrides it for one configured property or collection. This is independent of `WithMaxItems`, which limits the normal collection rendering.

A separate type profile can control the contents of an overlay:

```csharp
configuration.ConfigureDrillDown<WorkItem>(type =>
{
    type.ExcludeAll();
    type.Property(item => item.Name).AllowSet();
    type.Property(item => item.Owner).WithDrillDown();
});
```

When a registered object's runtime type has an explicit drilldown profile, its bag header also shows a drilldown control. This provides an entry point to the detailed profile when the normal `Configure<T>()` view intentionally exposes only a subset of the object's properties.

When no drilldown profile exists for the runtime type or its configured base types, rendering falls back to the normal `Configure<T>()` profile as a whole. Drilldown dialogs remain interactive: property setters, operations, and further drilldowns resolve through the ordered parent path chain. Multiple sibling and nested dialogs can remain open independently.

## Rate, date, and nested values

Specialized fluent calls can create the same strategies as their corresponding attributes:

```csharp
configuration.Configure<MyService>(type =>
{
    type.Rate(service => service.Requests)
        .Category("Traffic")
        .ShowRate()
        .ShowTotal();

    type.Date(service => service.Started)
        .ShowDate()
        .ShowElapsed();

    type.Extended(service => service.Connection)
        .Named("Connection");
});

configuration.Configure<ConnectionInfo>(type =>
{
    type.ExcludeAll();
    type.Include(connection => connection.Endpoint);
});
```

Nested objects rendered by `Extended` and collection categories resolve their own type configuration automatically.

## Scope and validation

Selectors must reference a direct property, such as `service => service.Status`. Nested expressions and method calls are rejected when configuration is built. Collection item selectors must also select direct item properties, and `WithMaxItems` must be greater than zero.

The initial fluent API configures instance properties. Existing attribute-based static property diagnostics continue to work unchanged.
