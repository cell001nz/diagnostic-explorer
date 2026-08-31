import { signal } from '@angular/core';
import { LogStreamEvent, LogStreamInitialization, LogStreamRoute, LogStreamRouteDestination, LogStreamRouteValue, LogStreamRoutingConfiguration } from '@domain/DiagResponse';
import { EventModel } from './EventModel';

export interface EventDestination {
    category: string;
    name: string;
}

export class ProcessEventStore {
    private static readonly defaultMaxEvents = 5_000;
    private static readonly defaultMaxAgeMinutes = 5;
    private static readonly stores = new Map<string, ProcessEventStore>();
    private readonly records = new Map<string, EventModel>();
    private readonly destinationKeys = new Map<string, readonly string[]>();
    private readonly destinationLabels = new Map<string, EventDestination>();
    private readonly destinationEvents = new Map<string, EventModel[]>();
    readonly events = signal<EventModel[]>([]);
    readonly routing = signal<LogStreamRoutingConfiguration>({ matchMode: 0, routes: [] });
    streamId = '';
    private maxEvents = ProcessEventStore.defaultMaxEvents;
    private maxAgeMinutes = ProcessEventStore.defaultMaxAgeMinutes;
    private nextPruneAt = 0;

    static forProcess(processId: string): ProcessEventStore {
        let store = this.stores.get(processId);
        if (!store) {
            store = new ProcessEventStore();
            this.stores.set(processId, store);
        }
        return store;
    }

    static pruneAll(): void {
        const now = Date.now();
        for (const store of this.stores.values()) store.prune(now);
    }

    static removeMissing(processIds: Iterable<string>): void {
        const activeProcessIds = new Set(processIds);
        for (const processId of this.stores.keys()) {
            if (!activeProcessIds.has(processId)) this.stores.delete(processId);
        }
    }

    reset(): void {
        this.streamId = '';
        this.records.clear();
        this.destinationKeys.clear();
        this.destinationLabels.clear();
        this.destinationEvents.clear();
        this.events.set([]);
        this.routing.set({ matchMode: 0, routes: [] });
    }

    initialize(initialization: LogStreamInitialization): void {
        if (!initialization?.streamId) return;

        if (this.streamId !== initialization.streamId) {
            this.streamId = initialization.streamId;
            this.records.clear();
            this.destinationKeys.clear();
            this.destinationLabels.clear();
        }

        this.routing.set(initialization.routing ?? { matchMode: 0, routes: [] });
        this.applyRetention(initialization);
        for (const event of initialization.replayEvents ?? []) this.insert(event);
        this.refresh();
    }

    append(events: readonly LogStreamEvent[]): EventModel[] {
        if (!this.streamId) return [];

        const receivedEvents: EventModel[] = [];
        let orderedEvents: EventModel[] | undefined;
        for (const event of events ?? []) {
            if (event.streamId !== this.streamId) continue;

            const addedEvent = this.insert(event);
            const storedEvent = addedEvent ?? this.records.get(`${event.streamId}\u001f${event.sequence}`);
            if (storedEvent) receivedEvents.push(storedEvent);
            if (!addedEvent) continue;

            orderedEvents ??= [...this.events()];
            this.insertBySequence(orderedEvents, addedEvent);
            this.routeEvent(addedEvent);
        }
        if (orderedEvents) {
            this.trimToMaxEvents(orderedEvents);
            this.events.set(orderedEvents);
        }
        if (Date.now() >= this.nextPruneAt) this.prune(Date.now());
        return receivedEvents;
    }

    eventsForDestination(category: string, name: string): EventModel[] {
        const destinationKey = ProcessEventStore.destinationKey(category, name);
        this.events();
        return this.destinationEvents.get(destinationKey) ?? [];
    }

    eventMatchesDestination(event: EventModel, category: string, name: string): boolean {
        const destinationKey = ProcessEventStore.destinationKey(category, name);
        return this.destinationKeys.get(this.eventKey(event))?.includes(destinationKey) ?? false;
    }

    configuredDestinations(): EventDestination[] {
        const destinations = new Map<string, EventDestination>();
        for (const route of this.routing().routes ?? []) {
            for (const destination of route.destinations ?? []) {
                if (!this.isFixed(destination.category) || !this.isFixed(destination.name)) continue;
                this.addDestination(destinations, destination.category.value, destination.name.value);
            }
        }
        return [...destinations.values()];
    }

    resolvedDestinations(): EventDestination[] {
        return [...this.destinationLabels.values()];
    }

    static destinationKey(category: string, name: string): string {
        return `${category}\u001f${name}`.toLocaleLowerCase();
    }

    private insert(source: LogStreamEvent): EventModel | undefined {
        const key = `${source.streamId}\u001f${source.sequence}`;
        if (this.records.has(key)) return undefined;

        const event = new EventModel(source);
        this.records.set(key, event);
        return event;
    }

    private refresh(now = Date.now()): void {
        const minimumDate = now - this.maxAgeMinutes * 60 * 1000;
        const ordered = [...this.records.values()]
            .filter((event) => new Date(event.date).valueOf() >= minimumDate)
            .sort((left, right) => right.sequence - left.sequence)
            .slice(0, this.maxEvents);
        const retained = new Set(ordered.map((event) => this.eventKey(event)));
        for (const key of this.records.keys()) {
            if (!retained.has(key)) {
                this.records.delete(key);
                this.destinationKeys.delete(key);
            }
        }

        this.destinationKeys.clear();
        this.destinationLabels.clear();
        this.destinationEvents.clear();
        for (const event of ordered) this.routeEvent(event);
        this.events.set(ordered);
        this.nextPruneAt = now + 60_000;
    }

    private applyRetention(initialization: LogStreamInitialization): void {
        const maxEvents = initialization.maxEvents;
        this.maxEvents = typeof maxEvents === 'number' && Number.isFinite(maxEvents) && maxEvents > 0 ? Math.floor(maxEvents) : ProcessEventStore.defaultMaxEvents;

        const maxAgeMinutes = initialization.maxAgeMinutes;
        this.maxAgeMinutes = typeof maxAgeMinutes === 'number' && Number.isFinite(maxAgeMinutes) && maxAgeMinutes > 0 ? maxAgeMinutes : ProcessEventStore.defaultMaxAgeMinutes;
    }

    private prune(now: number): void {
        const minimumDate = now - this.maxAgeMinutes * 60 * 1000;
        const retained = this.events().filter((event) => new Date(event.date).valueOf() >= minimumDate);
        if (retained.length === this.events().length) {
            this.nextPruneAt = now + 60_000;
            return;
        }

        const retainedEvents = new Set(retained);
        for (const event of this.events()) {
            if (!retainedEvents.has(event)) this.removeEvent(event);
        }
        this.events.set(retained);
        this.nextPruneAt = now + 60_000;
    }

    private trimToMaxEvents(events: EventModel[]): void {
        while (events.length > this.maxEvents) {
            const removed = events.pop();
            if (removed) this.removeEvent(removed);
        }
    }

    private routeEvent(event: EventModel): void {
        const eventKey = this.eventKey(event);
        const destinationKeys = this.resolveDestinations(event);
        this.destinationKeys.set(eventKey, destinationKeys);

        for (const destinationKey of destinationKeys) {
            const destinationEvents = [...(this.destinationEvents.get(destinationKey) ?? [])];
            this.insertBySequence(destinationEvents, event);
            this.destinationEvents.set(destinationKey, destinationEvents);
        }
    }

    private removeEvent(event: EventModel): void {
        const eventKey = this.eventKey(event);
        for (const destinationKey of this.destinationKeys.get(eventKey) ?? []) {
            const destinationEvents = this.destinationEvents.get(destinationKey);
            if (!destinationEvents) continue;

            const retainedEvents = destinationEvents.filter((existingEvent) => existingEvent !== event);
            if (retainedEvents.length === 0) {
                this.destinationEvents.delete(destinationKey);
                this.destinationLabels.delete(destinationKey);
            } else {
                this.destinationEvents.set(destinationKey, retainedEvents);
            }
        }

        this.records.delete(eventKey);
        this.destinationKeys.delete(eventKey);
    }

    private insertBySequence(events: EventModel[], event: EventModel): void {
        let low = 0;
        let high = events.length;
        while (low < high) {
            const middle = (low + high) >>> 1;
            if (events[middle].sequence > event.sequence) low = middle + 1;
            else high = middle;
        }
        events.splice(low, 0, event);
    }

    private resolveDestinations(event: EventModel): readonly string[] {
        const matches: LogStreamRoute[] = [];
        for (const route of this.routing().routes ?? []) {
            if (!this.matches(route, event)) continue;
            matches.push(route);
            if (route.stopProcessing) break;
        }

        const selected = this.selectRoutes(matches);
        const keys = new Set<string>();
        for (const route of selected) {
            for (const destination of route.destinations ?? []) {
                const category = this.resolveValue(destination.category, route, event.loggerCategory);
                const name = this.resolveValue(destination.name, route, event.loggerCategory);
                if (category && name) {
                    const key = ProcessEventStore.destinationKey(category, name);
                    keys.add(key);
                    this.destinationLabels.set(key, { category, name });
                }
            }
        }
        return [...keys];
    }

    private matches(route: LogStreamRoute, event: EventModel): boolean {
        if (route.minLevel != null && event.level < route.minLevel) return false;
        if (route.maxLevel != null && event.level > route.maxLevel) return false;

        const loggerName = event.loggerCategory ?? '';
        const matcher = route.loggerName ?? '';
        switch (this.getLoggerNameMatchMode(route)) {
            case 0:
                return this.equals(loggerName, matcher);
            case 1:
                return this.equals(loggerName, matcher) || (loggerName.length > matcher.length && loggerName.toLocaleLowerCase().startsWith(`${matcher.toLocaleLowerCase()}.`));
            case 2:
                return loggerName.toLocaleLowerCase().includes(matcher.toLocaleLowerCase());
            case 3:
                return true;
            default:
                return false;
        }
    }

    private selectRoutes(matches: LogStreamRoute[]): LogStreamRoute[] {
        switch (this.getRouteMatchMode()) {
            case 1:
                return matches.length === 0 ? [] : [matches.slice().sort((left, right) => right.loggerName.length - left.loggerName.length || left.order - right.order)[0]];
            case 2:
                return matches.length === 0 ? [] : [matches[0]];
            default:
                return matches;
        }
    }

    private resolveValue(value: LogStreamRouteValue, route: LogStreamRoute, loggerName: string): string | undefined {
        if (!value) return undefined;
        if (this.isFixed(value)) return value.value;
        if (this.getLoggerNameMatchMode(route) === 3) return loggerName;
        if (this.getLoggerNameMatchMode(route) !== 1 || loggerName.length <= route.loggerName.length) return undefined;
        return loggerName.substring(route.loggerName.length + 1);
    }

    private getRouteMatchMode(): number | undefined {
        switch (this.routing().matchMode) {
            case 'AllMatches':
                return 0;
            case 'MostSpecific':
                return 1;
            case 'FirstMatch':
                return 2;
            default:
                return this.routing().matchMode as number;
        }
    }

    private getLoggerNameMatchMode(route: LogStreamRoute): number | undefined {
        switch (route.loggerNameMatchMode) {
            case 'Exact':
                return 0;
            case 'Prefix':
                return 1;
            case 'Contains':
                return 2;
            case 'Wildcard':
                return 3;
            default:
                return route.loggerNameMatchMode as number;
        }
    }

    private isFixed(value: LogStreamRouteValue | undefined): boolean {
        return value?.source === 0 || value?.source === 'Fixed';
    }

    private eventKey(event: EventModel): string {
        return `${event.streamId}\u001f${event.sequence}`;
    }

    private addDestination(destinations: Map<string, EventDestination>, category: string | undefined, name: string | undefined): void {
        if (!category || !name) return;
        destinations.set(ProcessEventStore.destinationKey(category, name), { category, name });
    }

    private equals(left: string, right: string): boolean {
        return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
    }
}
