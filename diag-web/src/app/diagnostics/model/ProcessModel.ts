import { DiagnosticResponse, DrillDownEventMatcher, DrillDownEventViewDefinition, LogStreamEvent, LogStreamInitialization, OperationSet, PropertyBag } from '@domain/DiagResponse';
import { computed, inject, Injectable, signal } from '@angular/core';
import { CategoryModel } from './CategoryModel';
import { EventModel } from './EventModel';
import { customMerge } from '@util/merge';
import { ObservableDisposable } from '@model/ObservableDisposable';
import { Subject } from 'rxjs';
import { DiagnosticModelFactory } from '@model/DiagnosticModelFactory';
import { strEqCI } from '@util/stringUtil';
import { ProcessEventStore } from './ProcessEventStore';

function compareCategories(left: CategoryModel, right: CategoryModel): number {
    const leftIsSystem = strEqCI(left.name(), 'System');
    const rightIsSystem = strEqCI(right.name(), 'System');
    if (leftIsSystem !== rightIsSystem) return leftIsSystem ? -1 : 1;

    return left.name().localeCompare(right.name(), undefined, { sensitivity: 'base' });
}

@Injectable({ providedIn: 'root' })
export class ProcessModel implements ObservableDisposable {
    objectPaths: readonly string[] = Object.freeze([]);
    selectedEvent?: EventModel;
    titleMessage = signal('');
    categories = signal<CategoryModel[]>([]);
    operationSets = signal<OperationSet[]>([]);
    serverDate = signal<Date | undefined>(undefined);
    activeCatName = signal('');
    activeCat = computed(() => this.categories().find((c) => c.name() === this.activeCatName()));
    private eventStore?: ProcessEventStore;
    private drillDownEventViews: DrillDownEventViewDefinition[] = [];
    private includeGlobalEventViews = true;

    setObjectPaths(objectPaths: readonly string[]): void {
        this.objectPaths = Object.freeze([...objectPaths]);
    }

    setProcessId(processId: string, includeGlobalEventViews = true): void {
        this.eventStore = ProcessEventStore.forProcess(processId);
        this.includeGlobalEventViews = includeGlobalEventViews;
        this.reconcileEventViews();
    }

    initializeLogStream(initialization: LogStreamInitialization): void {
        this.eventStore?.initialize(initialization);
        this.reconcileEventViews();
    }

    appendLogStreamEvents(events: LogStreamEvent[]): void {
        const addedEvents = this.eventStore?.append(events) ?? [];
        this.reconcileEventViews();
        for (const category of this.categories()) {
            const categoryEvents = new Set<EventModel>();
            for (const sink of category.eventSinks()) {
                for (const event of sink.recordAddedEvents(addedEvents)) categoryEvents.add(event);
            }
            category.recordEventSeverity([...categoryEvents]);
        }
    }

    setDrillDownEventViews(eventViews: DrillDownEventViewDefinition[] | undefined): void {
        this.drillDownEventViews = eventViews ?? [];
        this.reconcileEventViews();
    }

    clear() {
        this.titleMessage.set('...');
        this.categories.set([]);
        this.operationSets.set([]);
        this.serverDate.set(undefined);
        this.activeCatName.set('');
    }

    resetEventStream(): void {
        this.eventStore?.reset();
        const categories = this.categories().filter((category) => {
            category.eventSinks.set([]);
            return category.bags().length > 0;
        });

        this.categories.set(categories);
    }

    public update(response: DiagnosticResponse) {
        this.titleMessage.set('Received ' + new Date().toLocaleTimeString());
        this.serverDate.set(new Date(response.serverDate));

        const catDataByName = new Map<string, { name: string; props: PropertyBag[] }>();
        for (const propertyBag of response.propertyBags) {
            const key = propertyBag.category.toLocaleLowerCase();
            const existing = catDataByName.get(key);
            if (existing) {
                existing.props.push(propertyBag);
            } else {
                catDataByName.set(key, { name: propertyBag.category, props: [propertyBag] });
            }
        }

        let cats = this.coalesceCategories(this.categories());
        for (const category of cats) {
            const key = category.name().toLocaleLowerCase();
            if (!catDataByName.has(key)) catDataByName.set(key, { name: category.name(), props: [] });
        }

        const catData = [...catDataByName.values()];

        customMerge(
            catData,
            cats,
            (d) => d.name.toLocaleLowerCase(),
            (c) => c.name().toLocaleLowerCase(),
            (d) => new CategoryModel(this, d.name, d.props),
            (d, c) => c.update(d.props),
            false
        );

        cats.sort(compareCategories);

        if (cats.filter((c) => !c.bags().length && !c.eventSinks().length)) cats = cats.filter((c) => c.bags().length || c.eventSinks().length);

        this.categories.set(cats);
        this.operationSets.set(response.operationSets);
        this.reconcileEventViews();

        if (!this.categories().some((category) => category.name() === this.activeCatName())) this.activeCatName.set(this.categories()[0]?.name() ?? '');
    }

    getOperationSet(setName: string | null): OperationSet | null {
        if (!setName) return null;

        return this.operationSets().find((s) => s.id === setName) ?? null;
    }

    private getCat(name: string): CategoryModel {
        let cat = this.categories().find((c) => strEqCI(c.name(), name));
        if (!cat) {
            cat = new CategoryModel(this, name);
            this.categories.update((categories) => [...categories, cat!].sort(compareCategories));
        }

        return cat;
    }

    private coalesceCategories(categories: CategoryModel[]): CategoryModel[] {
        const categoriesByName = new Map<string, CategoryModel>();
        for (const category of categories) {
            const key = category.name().toLocaleLowerCase();
            const existing = categoriesByName.get(key);
            if (!existing) {
                categoriesByName.set(key, category);
                continue;
            }

            const existingSinks = existing.eventSinks();
            const additionalSinks = category.eventSinks().filter((sink) => !existingSinks.some((existingSink) => strEqCI(existingSink.name, sink.name)));
            if (additionalSinks.length) existing.eventSinks.set([...existingSinks, ...additionalSinks]);
        }

        return [...categoriesByName.values()];
    }

    private reconcileEventViews(): void {
        const store = this.eventStore;
        if (!store) return;

        const addDestination = (category: string, name: string): void => {
            this.getCat(category).getSink(
                name,
                () => store.eventsForDestination(category, name),
                (event) => store.eventMatchesDestination(event, category, name)
            );
        };

        if (this.includeGlobalEventViews) {
            for (const destination of [...store.configuredDestinations(), ...store.resolvedDestinations()]) {
                addDestination(destination.category, destination.name);
            }
        }

        const drillDownEventCategory = this.includeGlobalEventViews
            ? undefined
            : (this.categories()
                  .find((category) => category.bags().length > 0)
                  ?.name() ?? 'DrillDown');
        const duplicateDrillDownViewNames = new Set(this.drillDownEventViews.filter((view) => this.drillDownEventViews.filter((candidate) => candidate.name === view.name).length > 1).map((view) => view.name));

        for (const view of this.drillDownEventViews) {
            const sinkName = !this.includeGlobalEventViews && duplicateDrillDownViewNames.has(view.name) ? `${view.category}: ${view.name}` : view.name;
            const matchesView = (event: EventModel): boolean => view.matchers.some((matcher) => this.matchesDrillDownEvent(event, matcher));
            this.getCat(drillDownEventCategory ?? view.category).getSink(sinkName, () => store.events().filter(matchesView), matchesView);
        }
    }

    private matchesDrillDownEvent(event: EventModel, matcher: DrillDownEventMatcher): boolean {
        if (matcher.minLevel != null && event.level < matcher.minLevel) return false;
        if (matcher.maxLevel != null && event.level > matcher.maxLevel) return false;

        const loggerName = event.loggerCategory.toLocaleLowerCase();
        const expected = matcher.loggerName.toLocaleLowerCase();
        switch (matcher.matchMode) {
            case 0:
                return loggerName === expected;
            case 1:
                return loggerName === expected || loggerName.startsWith(`${expected}.`);
            case 2:
                return loggerName.includes(expected);
            case 3:
                return true;
            default:
                return false;
        }
    }

    dispose(): void {}
    disposed$ = new Subject<true>();
}
