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
        type.OptIn();
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
            type.OptIn();
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

`OptIn()` excludes ordinary properties unless selected with `Include`, `Property`, `Collection`, `Rate`, `Date`, or `Extended`. Properties with a non-ignored diagnostic property attribute remain included.

`OptOut()` includes public properties by default. Existing `Browsable(false)` and ignored diagnostic properties remain excluded unless explicitly included.

The inclusion order is:

1. Explicit fluent `Include` or `Exclude`.
2. A diagnostic property attribute, including its `Ignore` value.
3. `BrowsableAttribute`.
4. Fluent `OptIn` or `OptOut`.
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

`Property`, `Collection`, `Rate`, `Date`, and `Extended` use `General` as their implicit category. `Include` only changes inclusion and leaves an otherwise unconfigured property uncategorized.

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

`WithMaxItems` limits concatenated, list, and category output. Count always reports the full collection size.

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
    type.OptIn();
    type.Include(connection => connection.Endpoint);
});
```

Nested objects rendered by `Extended` and collection categories resolve their own type configuration automatically.

## Scope and validation

Selectors must reference a direct property, such as `service => service.Status`. Nested expressions and method calls are rejected when configuration is built. Collection item selectors must also select direct item properties, and `WithMaxItems` must be greater than zero.

The initial fluent API configures instance properties. Existing attribute-based static property diagnostics continue to work unchanged.
