import {computed, inject, Injectable, signal} from '@angular/core';
import {MessageService} from 'primeng/api';
import {DiagHubService} from '@services/diag-hub.service';
import {RetroQuery, RetroSearchResult} from '@model/RetroQuery';
import {DiagnosticMsg} from '@model/DiagnosticMsg';
import {Level} from '@model/Level';
import {DiagProcess} from '@domain/DiagProcess';
import {getErrorMsg} from '@util/errorUtil';

function today(): Date {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d;
}

const DEFAULTS = {
    maxRecords: 5000,
    minLevel: 0,
    time: 7,
    hours: 12,
};

@Injectable({providedIn: 'root'})
export class RetroModel {

    readonly #hubService = inject(DiagHubService);
    readonly #messageService = inject(MessageService);

    // Search parameters
    maxRecords = signal(DEFAULTS.maxRecords);
    minLevel = signal(DEFAULTS.minLevel);
    date = signal<Date>(today());
    time = signal(DEFAULTS.time);
    timeZone = signal<'local' | 'utc'>('local');
    hours = signal(DEFAULTS.hours);
    machine = signal('');
    process = signal('');
    user = signal('');
    message = signal('');

    // Filter
    filterText = signal('');
    filterMinLevel = signal(0);

    // State
    results = signal<DiagnosticMsg[]>([]);
    selectedEvent = signal<DiagnosticMsg | null>(null);
    traceScopeVisible = signal(false);
    currentSearchId = signal(0);
    percentComplete = signal(0);
    titleMessage = signal('');

    #searchCount = 0;
    #searchStartTime?: Date;

    displayResults = computed<DiagnosticMsg[]>(() => {
        const all = this.results();
        const text = this.filterText().trim();
        const minLevel = this.filterMinLevel();

        if (!text && minLevel === 0)
            return all;

        let matcher: RegExp | undefined;
        if (text) {
            try {
                matcher = new RegExp(text, 'i');
            } catch {
                matcher = new RegExp(text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i');
            }
        }

        return all.filter(evt => {
            if (evt.level < minLevel)
                return false;
            if (!matcher)
                return true;
            return matcher.test(evt.message)
                || matcher.test(evt.detail ?? '')
                || matcher.test(evt.machine ?? '')
                || matcher.test(evt.user ?? '')
                || matcher.test(evt.process ?? '');
        });
    });

    resultsMessage = computed(() => {
        const total = this.results().length;
        const shown = this.displayResults().length;
        return shown === total
            ? `${total} events`
            : `${shown} of ${total} events`;
    });

    canDelete = computed(() => !this.currentSearchId() && this.displayResults().length > 0);

    constructor() {
        this.reset();
        this.#hubService.openHubConnection();

        this.#hubService.retroResults$.subscribe(result => {
            if (this.currentSearchId() === result.searchId)
                this.appendResponse(result);
        });

        this.#hubService.retroSearchEnd$.subscribe(searchId => {
            if (this.currentSearchId() === searchId)
                this.onSearchComplete(false);
        });

        this.#hubService.retroSearchError$.subscribe(({searchId, error}) => {
            console.log(error);
            if (this.currentSearchId() === searchId) {
                this.onSearchComplete(true);
                this.#messageService.add({severity: 'error', summary: 'Search error', detail: error, life: 3000});
            }
        });
    }

    reset(): void {
        this.maxRecords.set(DEFAULTS.maxRecords);
        this.minLevel.set(DEFAULTS.minLevel);
        this.date.set(today());
        this.time.set(DEFAULTS.time);
        this.timeZone.set('local');
        this.hours.set(DEFAULTS.hours);
        this.machine.set('');
        this.process.set('');
        this.user.set('');
        this.message.set('');
    }

    async searchProcess(item: DiagProcess): Promise<void> {
        this.reset();
        this.process.set(item.name);
        this.user.set(item.userName);
        this.machine.set(item.machineName);
        this.time.set(new Date().getHours() - 1);
        this.hours.set(2);
        await this.search();
    }

    prepareProcessSearch(item: DiagProcess): void {
        this.reset();
        this.process.set(item.name);
        this.user.set(item.userName);
        this.machine.set(item.machineName);
        this.date.set(today());
        this.time.set(new Date().getHours());
        this.hours.set(1);
    }

    async search(): Promise<void> {
        if (this.currentSearchId()) {
            const searchId = this.currentSearchId();
            this.onSearchComplete(true);
            await this.#hubService.cancelRetroSearch(searchId);
        } else {
            this.titleMessage.set('Searching...');
            const query = this.createSearchQuery();
            this.results.set([]);
            this.selectedEvent.set(null);
            this.traceScopeVisible.set(false);
            this.percentComplete.set(0);
            this.currentSearchId.set(++this.#searchCount);
            query.searchId = this.currentSearchId();
            this.#searchStartTime = new Date();
            console.log('Retro query sent to backend:', JSON.stringify(query, null, 2));
            await this.#hubService.startRetroSearch(query);
        }
    }

    async delete(): Promise<void> {
        if (this.currentSearchId())
            return;

        try {
            const toDelete = this.displayResults().map(m => m.msgId);

            if (!confirm(`Are you sure you want to delete ${toDelete.length} log entries?`))
                return;

            const deleted = await this.#hubService.deleteRecords(toDelete);
            this.#messageService.add({severity: 'success', summary: `${deleted} records deleted`, life: 2000});

            await this.search();
        } catch (err) {
            console.log(err);
            this.#messageService.add({severity: 'error', summary: 'Delete failed', detail: getErrorMsg(err), life: 3000});
        }
    }

    handleMouseOver(item: DiagnosticMsg, evt: MouseEvent): void {
        if (evt.buttons === 1)
            this.setCurrentEvent(item);
    }

    setCurrentEvent(item: DiagnosticMsg): void {
        const prev = this.selectedEvent();
        if (prev)
            prev.isSelected = false;

        item.isSelected = true;
        this.selectedEvent.set(item);
        this.traceScopeVisible.set(true);
    }

    hideTraceScope(): void {
        this.traceScopeVisible.set(false);
    }

    private createSearchQuery(): RetroQuery {
        const search = new RetroQuery();
        search.maxRecords = this.maxRecords();
        search.minLevel = this.minLevel();
        search.machine = this.machine();
        search.process = this.process();
        search.user = this.user();
        search.message = this.message();

        const d = this.date();
        const start = this.timeZone() === 'utc'
            ? new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate(), this.time(), 0, 0, 0))
            : (() => {
                const local = new Date(d);
                local.setHours(this.time(), 0, 0, 0);
                return local;
            })();
        search.startDate = start.toISOString();
        search.endDate = new Date(start.valueOf() + this.hours() * 60 * 60 * 1000).toISOString();

        return search;
    }

    private appendResponse(searchResult: RetroSearchResult): void {
        try {
            if (searchResult.searchId !== this.currentSearchId())
                return;

            const incoming = searchResult.results ?? [];
            if (incoming.length)
                this.results.update(current => current.concat(incoming));

            this.percentComplete.update(p => p + (1 - p) / 100);
            this.titleMessage.set(`Searching... ${this.results().length} records`);
        } catch (err) {
            console.log(err);
        }
    }

    private onSearchComplete(cancelled = false): void {
        const millis = new Date().valueOf() - (this.#searchStartTime?.valueOf() ?? Date.now());
        const time = millis > 1000
            ? (millis / 1000).toFixed(2) + 's'
            : millis + 'ms';

        this.#searchStartTime = undefined;
        this.currentSearchId.set(0);
        this.percentComplete.set(0);

        this.titleMessage.set(cancelled
            ? `Search cancelled after ${time}`
            : `Search complete in ${time}`);
    }

    protected readonly Level = Level;
}
