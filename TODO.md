# TODO

## Angular event-flow performance

- [ ] Add a bounded ingress queue and explicit backpressure/drop policy for `StreamLogEvents`. Frame batching is now used by both hub clients, but a hidden or overwhelmed tab can still accumulate an unbounded queue.
- [ ] Rework `ProcessEventStore` batch insertion and destination routing to avoid cloning and inserting into destination arrays for every event. Apply a client-side upper limit to server-provided `maxEvents`.
- [ ] Skip `ProcessModel.reconcileEventViews()` and cross-sink fan-out when a stream delivery contains no newly stored events. Reconcile only when event routing configuration or resolved destinations change.
- [ ] Release `EventSinkModel.eventNumbers` entries as events are evicted, and bound or retire dynamically discovered empty destinations/sinks.
- [ ] Virtualize event-grid rows and defer inactive category panels. Avoid rescanning and rendering every retained row for each filter or event update.
- [ ] Replace per-grid 250ms rate timers with work limited to visible grids or a shared clock. Avoid the one-item sort in `getEventNumber` for each rendered row.
- [ ] Parse trace scopes lazily when event details are opened rather than for every received event.
