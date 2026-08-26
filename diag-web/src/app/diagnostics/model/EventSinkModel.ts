import {SystemEvent} from '@domain/DiagResponse';
import {CategoryModel} from './CategoryModel';
import {EventModel} from './EventModel';
import {FilterCriteria} from './FilterCriteria';
import {computed, Signal, signal} from '@angular/core';
import {Level} from './Level';

import pluralize from 'pluralize-esm';

export class EventSinkModel {
    name = '';
    events: Signal<EventModel[]>;
    filteredEvents: Signal<EventModel[]>;
    isCollapsed = signal(false)

    filterVisible = false;
    watchEnabled = false;

    filterCriteria = new FilterCriteria();

    /** Whether the filter flyout panel is visible */
    filtersVisible = signal(false);
    /** Text filter applied to event messages */
    filterText = signal('');
    /** Minimum level to show (0 = Trace … 6 = None) */
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
        }
    }

    setFilterText(text: string): void {
        this.filterText.set(text);
    }

    setMinLevel(level: number): void {
        this.minLevel.set(level);
    }

    constructor(readonly cat: CategoryModel, name: string, private readonly eventProvider: () => EventModel[] = () => []) {
        this.watchEnabled = true;
        this.name = name;
        this.events = computed(() => this.eventProvider());
        this.filteredEvents = computed(() => {
            const events = this.events();
            const text = this.filterText().trim().toLowerCase();
            const minLevel = this.minLevel();
            return events.filter((event) =>
                (minLevel === 0 || event.level >= minLevel) &&
                (!text || event.message?.toLowerCase().includes(text) || event.detail?.toLowerCase().includes(text))
            );
        });
    }

    get message(): string {
        return pluralize('events', this.events().length, true);
    }

    toggleCollapsed() {
        this.isCollapsed.update(v => !v);
    }

    public addEvents(evts: SystemEvent[]): void {
        void evts;
    }

    public clearEvents(): void {
        // Event arrays are projections over the owning ProcessEventStore.
    }

    private onCriteriaChanged(): void {
        this.filterEvents();
    }

    private filterEvents(): void {
        // Filters are computed from source events and filter signals.
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
