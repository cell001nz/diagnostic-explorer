# Drilldowns

A drilldown opens a focused live view of a diagnostic value that represents an object or collection. It lets an operator move from a summary property into that value's own diagnostics without leaving the current diagnostic session.

Drilldowns are opt-in. A diagnostic property, collection item, or category is shown as drillable only when its configuration enables drilldown and its current value can be inspected. Scalar values remain ordinary text.

## How They Work

Selecting a drilldown opens a new popup containing the selected value's diagnostics. The popup can contain the same kinds of information as the main view: categories, properties, alerts, editable values, and executable operations. A value inside that popup can open another drilldown, forming a chain of focused views.

Each popup is independent. It refreshes while open and can be closed with its Close button without affecting other drilldowns. Pressing Escape closes only the topmost drilldown. Clicking outside all drilldown popups dismisses the complete open drilldown stack.

The popup title is the inspected object's diagnostic bag name. The outer object frame is deliberately omitted so its categories are the primary content.

## Technical Note

Drilldowns are addressed by an ordered `objectPaths` chain, not by stored object references or opaque IDs. The chain identifies the inspected value from the registered diagnostics and is passed with nested property-edit and operation requests so actions run against the correct live object.

When changing drilldown UI behavior, preserve these rules:

- Opening a nested drilldown appends the selected node's normal diagnostic path to the existing `objectPaths` chain.
- A popup owns its own `ProcessModel` and refresh lifecycle.
- Explicit close actions are local; only an outside-dialog click dismisses every active drilldown.
- The active-dialog stack is managed in `CategoryViewComponent`; its newest dialog is the topmost dialog for Escape handling.
