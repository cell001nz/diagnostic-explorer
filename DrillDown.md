# Drilldowns

A drilldown opens a focused live view of a diagnostic value that represents an object or collection. It lets an operator move from a summary property into that value's own diagnostics without leaving the current diagnostic session.

Drilldowns are opt-in. A diagnostic property, collection item, or category is shown as drillable only when its configuration enables drilldown and its current value can be inspected. Scalar values remain ordinary text.

## How They Work

Selecting a drilldown opens a new popup containing the selected value's diagnostics. The popup can contain the same kinds of information as the main view: categories, properties, alerts, editable values, and executable operations. A value inside that popup can open another drilldown, forming a chain of focused views.

Each popup is independent. It refreshes while open and can be closed with its Close button without affecting other drilldowns. Pressing Escape closes only the topmost drilldown. Clicking outside all drilldown popups dismisses the complete open drilldown stack.

The popup title is the inspected object's diagnostic bag name. The outer object frame is deliberately omitted so its categories are the primary content.

## Planned Event Views

A drilldown specification may define any number of event tables. These are views over the process's unified raw event stream, not independent transport streams or retained buffers. Consequently the same event may appear in the main diagnostics page and in several open drilldowns without being copied at the process or server.

Drilldown routes support both static logger-name patterns and patterns derived from the inspected instance. The proposed API shape is:

```csharp
config.ConfigureDrillDown<Widget>(options =>
{
	options.OptIn();
	options.Property(widget => widget.Name);

	options.Route(
		widget => widget.LoggerName,
		LoggerNameMatchMode.Exact,
		route => route
			.AtLeast(LogLevel.Information)
			.To("Events", "Widget Events"));

	options.Route(
		widget => widget.LoggerName,
		LoggerNameMatchMode.Exact,
		route => route
			.AtLeast(LogLevel.Error)
			.To("Events", "Widget Errors"));
});
```

In WidgetSample, `LoggerName` should be captured when the adapter-specific logger is created and contain the complete category, for example `WidgetSample.Harness.Widget.Widget X(1)`. It must remain stable even if the mutable widget `Name` later changes. Computing the matcher from the current `FullName` would lose events written under the original logger category.

Each `Route` call defines a table projection. The logger matcher, level bounds, and destinations are independent, so a drilldown can contain multiple event tables and an event may match more than one table. Match behavior is explicit: `Exact` is appropriate for a complete instance logger name; `Prefix` includes that logger and hierarchical descendants. Arbitrary partial matching should use an explicit `Contains` mode if it is required rather than changing prefix semantics.

The static overload remains useful for aggregate views. For example, `options.Route(typeof(Widget).FullName, LoggerNameMatchMode.Prefix, ...)` matches all WidgetSample widget loggers, whereas `options.Route(widget => widget.LoggerName, LoggerNameMatchMode.Exact, ...)` narrows the same table definition to the selected widget instance.

Instance expressions are evaluated by the process when resolving the drilldown and only the resulting serializable matcher definitions are returned to Angular. For a collection drilldown, definitions with the same destination are combined by OR-ing the resolved instance matchers. A collection of widgets can therefore show one `Widget Events` table containing events from every displayed widget, while drilling into one widget resolves the same configuration to a table containing only that widget's events.

Drilldown routes are presentation filters only. The corresponding logger categories must also be admitted by the process-wide `ConfigureEventRouting` rules; opening a drilldown does not change provider enablement or begin capturing previously excluded events.

## Technical Note

Drilldowns are addressed by an ordered `objectPaths` chain, not by stored object references or opaque IDs. The chain identifies the inspected value from the registered diagnostics and is passed with nested property-edit and operation requests so actions run against the correct live object.

When changing drilldown UI behavior, preserve these rules:

- Opening a nested drilldown appends the selected node's normal diagnostic path to the existing `objectPaths` chain.
- A popup owns its own `ProcessModel` and refresh lifecycle.
- All popups for a process read from one shared canonical process event store; a popup owns only its route projections, filters, selection, and display state.
- Explicit close actions are local; only an outside-dialog click dismisses every active drilldown.
- The active-dialog stack is managed in `CategoryViewComponent`; its newest dialog is the topmost dialog for Escape handling.
