## Plan: Replayable Unified Logging Stream

Replace per-destination `EventSink` accumulation with one raw logging stream per process. The process keeps a bounded five-minute source-event buffer, uses `ConfigureEventRouting` only to admit events, and sends the ordered route configuration plus raw replay snapshot when a stream subscription starts. DiagnosticService maintains one corresponding per-process relay buffer and one upstream process subscription, while each browser has only a small independent outbound queue. Angular evaluates the routing rules, stores every source event once, and projects that canonical buffer into configured sinks plus overlapping `All Events`, `Warnings`, and `Errors` views.

**Steps**

### Phase 1: Raw event, route, and replay contracts

1. Introduce a source-oriented `LogStreamEvent` DTO in `DiagnosticExplorer` with `StreamId`, monotonic `Sequence`, UTC timestamp, logger category, level, message, detail, and primitive EventId fields. It must not contain a single `SinkName`/`SinkCategory`; one raw event must have one identity regardless of how many client views display it.
2. Introduce a JSON/SignalR-friendly `LogRoutingConfiguration` snapshot containing match mode and ordered routes, including category pattern, min/max levels, stop-processing, and destination `RouteValue` source/value. Create it from `DiagnosticManager.CurrentConfiguration.RuntimeOptions.Routing`; do not expose mutable runtime option instances directly.
3. Replace per-sink retention for provider-routed logs with a thread-safe process-global `LogEventStore`. It records every admitted event even when no server/browser is subscribed, retains the latest five minutes with a hard 5,000-event cap (whichever limit is reached first), and assigns sequence numbers. `CreateSubscription()` must atomically register a bounded live channel and capture `{ streamId, routingConfiguration, replayEvents, highWatermark }`, preventing loss or duplication between snapshot and live delivery. Keep direct `EventSink`/`EventSinkRepo` only for explicit DiagnosticService operational sinks and retro behavior.
4. Refactor `EventSinkRouter` into the process-side admission gate: retain the existing matcher for `ILogger.IsEnabled` and verify again on publish, but publish one raw event rather than writing one `SystemEvent` per resolved destination. Preserve all current matching semantics because Angular must implement the same contract. _Depends on steps 1-3._
5. Update Microsoft.Extensions.Logging, Serilog, NLog, and log4net adapters to publish through the raw event store/router instead of `EventSinkRepo`, preserving category, level, message/detail, scopes, exceptions, and EventId normalization. Remove repository-injection APIs and dual writes because backward compatibility is not required. _Depends on step 4._

### Phase 2: Process-to-server initialization and live stream

6. Replace process-side `SetEvents`/`StreamEvents(SystemEvent[])` with `InitializeLogStream(LogStreamInitialization)` and `StreamLogEvents(LogStreamEvent[])`. Initialization carries the route snapshot, replay buffer, stream ID, and high-water sequence; both callbacks describe one logical stream, with initialization always sent before live batches.
7. Change `HubServerAdapter.SubscribeEvents` to create one atomic `LogEventStore` subscription, send its initialization, and then send buffered live batches (approximately 50 ms/100 events). Disposal/unsubscribe cancels only that host subscription; the process replay buffer continues collecting events. A process configured with multiple remote host URLs has one SignalR connection/subscription per host, while each individual DiagnosticService has one connection to that process instance. _Depends on phase 1._
8. Update `IDiagnosticHubServer`, `DiagnosticHub`, `IDiagnosticClient`, and `DiagnosticClientHandler` to expose one initialization subject and one live-batch subject. Preserve `Registration.InstanceId` as the stable stream identity across network reconnects for the lifetime of the process-hosting service; a process restart gets a new stream ID and reset sequence.

### Phase 3: One server relay buffer, many browser queues

9. Replace `DiagnosticSubscription._eventRepo` with one per-process `LogEventStore`-like relay buffer holding raw events and the latest routing snapshot. It uses the same five-minute/5,000-event bounds, merges process initialization by `(StreamId, Sequence)`, and appends live batches. On the same stream ID it deduplicates replay after process reconnection; on a new stream ID it resets the relay buffer and notifies browsers to replace their state.
10. Keep exactly one upstream process subscription per `DiagnosticSubscription`, regardless of browser count. When the first browser subscribes, start/await process initialization when online, merge it into the relay store, then atomically initialize the browser from the relay snapshot. Additional browsers initialize from the same relay store and do not create more process connections or replay buffers. If the process is offline, initialize from any unexpired server relay data.
11. Give each `WebClientHandler` its own small bounded outbound queue and sender task. Incoming process batches are enqueued to every subscribed handler, so a slow browser cannot block the process stream or other browsers. The queue contains only pending outbound frames, never the five-minute history. On overflow, discard pending deltas and schedule a fresh atomic initialization from the shared relay buffer rather than silently losing ordering; dispose the queue on browser disconnect, unsubscribe, or process switch.
12. Replace browser callbacks with `InitializeLogStream(processId, initialization)` and `StreamLogEvents(processId, events)`. Add an explicit `WebHub.Unsubscribe` operation; fix `DiagHubService.unsubscribeProcess`, which currently invokes `Subscribe`. _Depends on steps 9-11._

### Phase 4: Self-host parity

13. Use the process `LogEventStore` directly in `SelfHostManager`. `SubscribeAsync` sends the same route snapshot plus five-minute replay atomically, then the manager fans live batches to independent client queues. Remove `EventSinkRepo.Default.GetEvents()` and the old `SetEvents` callback from modern and net48 self-host contracts/Owin adapters.
14. Preserve the existing self-host `onreconnected` resubscription behavior for SignalR Core and SignalR 2. A reconnect receives a fresh initialization; Angular merges it when `StreamId` is unchanged and replaces state when it changed. _Parallel with phase 3 after phase 2._

### Phase 5: Angular routing and canonical projections

15. Add frontend DTOs for raw events, route snapshots, route values, and initialization. Update `DiagHubService` and `SelfHostHubService` to register initialization and live callbacks. Track the currently selected process in the central service and call `Subscribe` again from `onreconnected`; central automatic reconnect currently creates a new server connection without restoring its process subscription.
16. Implement a focused TypeScript route matcher mirroring C# exactly: case-insensitive dot-boundary category matching, wildcard behavior, level bounds, ordered first match, most-specific tie-breaking, all matches, stop-processing, destination deduplication, and `LoggerSuffix` resolution. Use shared/golden routing cases in .NET and Angular tests to prevent semantic drift. Sending rules rather than destinations is preferred here because it avoids repeated destination metadata on every event, lets buffered events be reprojected when rules change, and gives Angular full ownership of display fan-out.
17. Make `ProcessModel` own one canonical `EventModel[]` signal keyed by `(StreamId, Sequence)`. Initialization merges replay without duplicates for the same stream, replaces state for a new stream, and updates routing before projection; live batches append in order without mutating payload arrays. Keep the browser cap aligned with the stream’s 5,000-event maximum and prune events older than five minutes.
18. Convert `EventSinkModel` into a computed view over canonical events instead of owning arrays. For each event, evaluate routes once in the client and cache its derived destination keys; configured sink views filter by those keys. Add synthetic views over the same objects: all admitted events, exactly Warning, and Error-or-higher including Critical. Preserve text/min-level filters, selection, counts, collapse state, detail panels, and severity pulses.
19. Build configured categories/sinks from the routing snapshot where destinations are fixed and lazily add dynamic `LoggerSuffix` destinations as events resolve. Use a reserved internal category ID for a displayed “Live Events” category containing “All Events”, “Warnings”, and “Errors”, avoiding collisions with configured diagnostic categories.
20. If a routing snapshot changes for the same stream, replace the matcher, recompute cached destination keys for the retained raw buffer, reconcile visible sink/category models, and keep the underlying event objects and selection where still valid.

### Phase 6: Documentation, tests, and packaged viewers

21. Update the sample and documentation: `ConfigureEventRouting` is evaluated process-side for admission and client-side for categorization; route rules are sent on every initialization/reconnection; one event may appear in multiple configured and synthetic views; replay retention is one five-minute raw buffer rather than per-sink storage.
22. Rewrite routing/adapter tests to assert one raw publication per accepted source event and no publication for unmatched events. Add process-store tests for age/count retention, atomic snapshot-to-live handoff, ordering, multiple host subscribers, overflow, cancellation, and disposal. Keep existing `EventRetentionTests` only for intentional direct `EventSink` behavior.
23. Add DiagnosticService tests for one upstream process subscription with multiple browsers, independent queues, shared replay storage, slow-client resynchronization, first/second browser initialization, browser reconnect, process reconnect with same stream ID, process restart with new stream ID, offline replay, and teardown.
24. Add Angular tests for C#/TypeScript routing parity, replay/live deduplication, stream replacement, route updates, canonical retention, overlapping configured/synthetic views, central reconnect resubscription, correct unsubscribe, and self-host reconnect parity.
25. Remove dead sink-bound realtime DTOs and callbacks, rebuild normal/self-host/net48 Angular assets into `DiagnosticExplorer.Hosting/wwwroot`, then run full .NET/Angular validation and manually exercise two simultaneous browsers against both DiagnosticService and self-host modes.

**Relevant files**

- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer/Logging/EventSinkRouter.cs` and `EventSinkRouteOptions.cs` — process admission semantics and route snapshot source.
- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer/Logging/EventSinkLogEvent.cs` — source event data feeding the raw stream.
- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer/Events/EventSinkRepo.cs`, `EventSinkStream.cs`, and `EventRetentionOptions.cs` — patterns to replace for application logs; retain only direct operational sink behavior.
- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer/DiagnosticManager.cs` — current configuration and process-global store lifecycle.
- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer.Extensions.Logging/DiagnosticExplorerLogger.cs` and `DiagnosticExplorerLoggerProvider.cs` — primary provider admission/publication path.
- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer.Serilog/DiagnosticExplorerSink.cs`, `DiagnosticExplorer.NLog/DiagnosticExplorerTarget.cs`, and `DiagnosticExplorer.Log4Net/RoutingDiagnosticAppender.cs` — adapter parity.
- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer/Interface/IDiagnosticHubClient.cs` and `DiagnosticExplorer.Hosting/HubServerAdapter.cs` — process-to-service initialization/live protocol.
- `d:/_Repos/diagnostic-explorer/DiagnosticService/Hubs/DiagnosticHub.cs`, `WebHub.cs`, and `IWebHubClient.cs` — central SignalR contracts and explicit unsubscribe.
- `d:/_Repos/diagnostic-explorer/DiagnosticService/ClientHandlers/DiagnosticClientHandler.cs`, `DiagnosticSubscription.cs`, and `WebClientHandler.cs` — shared process relay and per-browser queues.
- `d:/_Repos/diagnostic-explorer/DiagnosticExplorer.Hosting/SelfHost/SelfHostContracts.cs`, `SelfHostManager.cs`, and `OwinSelfHost.cs` — self-host replay/live parity.
- `d:/_Repos/diagnostic-explorer/diag-web/src/app/services/diag-hub.service.ts` and `diag-web/src/self-host/self-host-hub.service.ts` — callback contracts and reconnect resubscription.
- `d:/_Repos/diagnostic-explorer/diag-web/src/app/domain/DiagResponse.ts` — replace sink-bound realtime DTOs.
- `d:/_Repos/diagnostic-explorer/diag-web/src/app/diagnostics/model/ProcessModel.ts`, `CategoryModel.ts`, `EventSinkModel.ts`, and `EventModel.ts` — canonical storage, client route evaluation, and projections.
- `d:/_Repos/diagnostic-explorer/samples/WidgetSample.Harness/DiagnosticsConfiguration.cs` and `Docs/` — explain and demonstrate revised routing semantics.

**Verification**

1. Run `dotnet test DiagnosticExplorer.Logging.Tests/DiagnosticExplorer.Logging.Tests.csproj` plus the new service/self-host tests.
2. Run the workspace `Build Development` task to compile all targets and regenerate packaged viewer assets.
3. In `diag-web`, run `npm test -- --watch=false`, `npm run build`, `npm run build:self-host`, and `npm run build:self-host-net48`.
4. Verify source no longer uses `SetEvents`, sink-bound realtime `StreamEvents`, or `EventSinkRepo` in provider/transport/relay paths.
5. Run WidgetSample through DiagnosticService with two browsers. Confirm one process connection/upstream subscription, independent live delivery, identical five-minute initialization, overlapping views, browser reconnect replay/deduplication, and unaffected delivery when one browser is throttled/disconnected.
6. Interrupt and restore the process-to-server connection without restarting the process; verify server merge by stream/sequence. Then restart the process and verify a new stream replaces stale browser state.
7. Repeat replay/reconnect behavior in modern and net48 self-host viewers.

**Decisions**

- No backward-compatible contracts, constructor overloads, or dual-write transition are required.
- Routing rules, not resolved destinations, are sent on every initialization. C# remains the admission filter required by provider `IsEnabled`; Angular owns category/sink distribution.
- The default replay window is five minutes with a 5,000-event hard cap, configurable at the process and mirrored by the server relay.
- Replay buffers exist once at the process and once per process at DiagnosticService, never once per sink or browser. Browser queues are small transient backpressure boundaries only.
- One DiagnosticService has one SignalR connection to each registered process instance and one event subscription for that process, then fans out to any number of browser SignalR connections. Multiple configured diagnostic hosts intentionally create one process connection per host.
- Initialization and live subscription are atomic at each buffer boundary, and `(StreamId, Sequence)` provides replay/live deduplication.
- Retro search/storage and explicit DiagnosticService operational `EventSink`s remain outside this migration.
