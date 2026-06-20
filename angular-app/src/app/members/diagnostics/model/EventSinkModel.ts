import {SystemEvent} from '@domain/DiagResponse';
import {CategoryModel} from './CategoryModel';
import {EventModel} from './EventModel';
import {FilterCriteria} from './FilterCriteria';
import {signal} from '@angular/core';
import {Level} from './Level';

import pluralize from 'pluralize-esm';

export class EventSinkModel {
    name = '';
    events = signal<EventModel[]>([]);
    filteredEvents = signal<EventModel[]>([]);
    private latestReceived = 0;
    message = '';
    isCollapsed = signal(false)

    filterVisible = false;
    watchEnabled = false;

    filterCriteria = new FilterCriteria();

    /** Whether the filter flyout panel is visible */
    filtersVisible = signal(false);
    /** Text filter applied to event messages */
    filterText = signal('');
    /** Minimum level to show (0 = Verbose … 7 = Fatal) */
    minLevel = signal(0);

    /** Convenience: the label for the current minLevel value */
    get minLevelLabel(): string {
        return Level.LevelToString(this.minLevel());
    }

    toggleFilters(): void {
        const closing = this.filtersVisible();
        this.filtersVisible.update(v => !v);
        if (closing) {
            this.filterText.set('');
            this.minLevel.set(0);
            this.filterEvents();
        }
    }

    setFilterText(text: string): void {
        this.filterText.set(text);
        this.filterEvents();
    }

    setMinLevel(level: number): void {
        this.minLevel.set(level);
        this.filterEvents();
    }

    constructor(readonly cat: CategoryModel, name: string) {
        this.watchEnabled = true;
        this.name = name;
    }

    toggleCollapsed() {
        this.isCollapsed.update(v => !v);
    }

    public addEvents(evts: SystemEvent[]): void {

        // this.latestReceived = evt;

        const evtModels = evts.map(evt => new EventModel(evt));
        const currentEvents = this.events();

        let newEvents: EventModel[];
        if (this.filterCriteria.isBlank) {
            newEvents = [...evtModels, ...currentEvents];
        } else {
            newEvents = [...evtModels, ...currentEvents];
        }
        if (newEvents.length > 500)
            newEvents = newEvents.slice(0, 500);

        this.events.set(newEvents);
        this.filterEvents();
    }

    public clearEvents(): void {
        this.events.set([]);
        this.filteredEvents.set([]);
        this.message = '';
    }

    private onCriteriaChanged(): void {
        this.filterEvents();
    }

    private filterEvents(): void {
        const currentEvents = this.events();
        const text = this.filterText().trim().toLowerCase();
        const minLvl = this.minLevel();

        let filtered = currentEvents;

        // Apply min-level filter
        if (minLvl > 0) {
            filtered = filtered.filter(evt => evt.level >= minLvl);
        }

        // Apply text filter
        if (text) {
            filtered = filtered.filter(evt =>
                evt.message?.toLowerCase().includes(text) ||
                evt.detail?.toLowerCase().includes(text)
            );
        }

        this.filteredEvents.set(filtered);

        if (text || minLvl > 0) {
            this.message = `${filtered.length} of ${currentEvents.length} ` + pluralize('event', currentEvents.length);
        } else {
            this.message = pluralize('events', currentEvents.length, true);
        }

        if (this.latestReceived)
            this.message += ` (+${this.latestReceived})`;
    }

    private onFilterVisibleChanged(): void {
        this.filterEvents();
    }

    handleDoubleClick(evt: MouseEvent) {
        if (evt.detail === 2) {
            this.isCollapsed.set(false);
            this.cat.eventSinks().forEach(c => c.isCollapsed.set(c !== this));
            this.cat.bags().forEach(c => c.isCollapsed.set(true));
        }
    }
}
