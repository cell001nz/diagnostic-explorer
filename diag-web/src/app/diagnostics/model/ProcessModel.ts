import { DiagnosticResponse, DrillDownEventMatcher, DrillDownEventViewDefinition, LogStreamEvent, LogStreamInitialization, OperationSet, PropertyBag } from '@domain/DiagResponse';
import * as _ from 'lodash-es';
import { computed, inject, Injectable, signal } from '@angular/core';
import { CategoryModel } from './CategoryModel';
import { EventModel } from './EventModel';
import { customMerge } from '@util/merge';
import { ObservableDisposable } from '@model/ObservableDisposable';
import { Subject } from 'rxjs';
import { DiagnosticModelFactory } from '@model/DiagnosticModelFactory';
import { strEqCI } from '@util/stringUtil';
import { ProcessEventStore } from './ProcessEventStore';

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
        this.eventStore?.append(events);
        this.reconcileEventViews();
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

    public update(response: DiagnosticResponse) {
        this.titleMessage.set('Received ' + new Date().toISOString().substring(11, 19) + ' UTC');
        this.serverDate.set(new Date(response.serverDate));

        const bagCats: { [key: string]: PropertyBag[] } = _.groupBy(response.propertyBags, (p) => p.category);

        const catData: { name: string; props: PropertyBag[] }[] = _.uniq(_.keys(bagCats).concat(this.categories().map((c) => c.name()))).map((name) => ({ name, props: bagCats[name] ?? [] }));

        let cats = this.categories().slice();

        customMerge(
            catData,
            cats,
            (d) => d.name,
            (c) => c.name(),
            (d) => new CategoryModel(this, d.name, d.props),
            (d, c) => c.update(d.props),
            false
        );

        cats = _.sortBy(cats, (c) => c.name());

        if (cats.filter((c) => !c.bags().length && !c.eventSinks().length)) cats = cats.filter((c) => c.bags().length || c.eventSinks().length);

        this.categories.set(cats);
        this.operationSets.set(response.operationSets);
        this.reconcileEventViews();

        if (!this.activeCatName() && this.categories().length) this.activeCatName.set(this.categories()[0].name());
    }

    getOperationSet(setName: string | null): OperationSet | null {
        if (!setName) return null;

        return this.operationSets().find((s) => s.id === setName) ?? null;
    }

    private getCat(name: string): CategoryModel {
        let cat = this.categories().find((c) => strEqCI(c.name(), name));
        if (!cat) {
            cat = new CategoryModel(this, name);
            this.categories.update((categories) => _.sortBy([...categories, cat!], (c) => c.name()));
        }

        return cat;
    }

    private reconcileEventViews(): void {
        const store = this.eventStore;
        if (!store) return;

        const addDestination = (category: string, name: string): void => {
            this.getCat(category).getSink(name, () => store.eventsForDestination(category, name));
        };

        if (this.includeGlobalEventViews) {
            for (const destination of [...store.configuredDestinations(), ...store.resolvedDestinations()]) {
                addDestination(destination.category, destination.name);
            }
        }

        const drillDownEventCategory = this.includeGlobalEventViews
            ? undefined
            : this.categories().find((category) => category.bags().length > 0)?.name() ?? 'DrillDown';
        const duplicateDrillDownViewNames = new Set(
            this.drillDownEventViews
                .filter((view) => this.drillDownEventViews.filter((candidate) => candidate.name === view.name).length > 1)
                .map((view) => view.name)
        );

        for (const view of this.drillDownEventViews) {
            const sinkName = !this.includeGlobalEventViews && duplicateDrillDownViewNames.has(view.name)
                ? `${view.category}: ${view.name}`
                : view.name;
            this.getCat(drillDownEventCategory ?? view.category).getSink(sinkName, () =>
                store.events().filter((event) => view.matchers.some((matcher) => this.matchesDrillDownEvent(event, matcher)))
            );
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
