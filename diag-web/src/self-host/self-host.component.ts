import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CategoryViewComponent } from '@app/diagnostics/category-view/category-view.component';
import { EventDetailPanelComponent } from '@app/diagnostics/event-detail-panel/event-detail-panel.component';
import { EventModel } from '@model/EventModel';
import { ProcessModel } from '@model/ProcessModel';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { SelfHostDiagHubService } from './self-host-hub.service';

const LOCAL_PROCESS_ID = 'self';

@Component({
    selector: 'app-self-host',
    imports: [Tabs, TabPanel, TabList, Tab, TabPanels, CategoryViewComponent, EventDetailPanelComponent],
    templateUrl: './self-host.component.html',
    styleUrl: './self-host.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SelfHostComponent implements OnInit, OnDestroy {
    readonly hub = inject(SelfHostDiagHubService);
    readonly connectionState = this.hub.connectionState;
    readonly error = this.hub.error;
    readonly process = this.hub.process;
    readonly processModel = signal(new ProcessModel());
    readonly selectedEvent = signal<EventModel | null>(null);
    readonly detailHeight = signal(200);
    #resizeStartY = 0;
    #resizeStartHeight = 0;

    constructor() {
        this.hub.clearEvents$.pipe(takeUntilDestroyed()).subscribe(() => this.processModel().clearEvents());
        this.hub.loadEvents$.pipe(takeUntilDestroyed()).subscribe((data) => this.processModel().loadEvents(data));
        this.hub.streamEvents$.pipe(takeUntilDestroyed()).subscribe((data) => this.processModel().streamEvents(data.events));
        this.hub.diagsArrived$.pipe(takeUntilDestroyed()).subscribe((data) => this.processModel().update(data.response));

        window.addEventListener('keydown', this.onWindowKeyDown, true);
    }

    async ngOnInit(): Promise<void> {
        try {
            await this.hub.openHubConnection();
            await this.refresh();
        } catch {}
    }

    ngOnDestroy(): void {
        window.removeEventListener('keydown', this.onWindowKeyDown, true);
        void this.hub.stop();
    }

    async refresh(): Promise<void> {
        await this.hub.subscribeProcess(LOCAL_PROCESS_ID);
    }

    onCategoryChange(category: string | number | undefined): void {
        if (category != null) this.processModel().activeCatName.set(String(category));
    }

    expandCollapse(): void {
        this.processModel().activeCat()?.expandCollapse();
    }

    onEventSelected(event: EventModel): void {
        const previous = this.selectedEvent();
        if (previous) previous.isSelected = false;
        if (previous === event) {
            this.selectedEvent.set(null);
        } else {
            event.isSelected = true;
            this.selectedEvent.set(event);
        }
    }

    closeDetail(): void {
        const selected = this.selectedEvent();
        if (selected) selected.isSelected = false;
        this.selectedEvent.set(null);
    }

    private onWindowKeyDown = (event: KeyboardEvent): void => {
        if (event.key !== 'Escape' || !this.selectedEvent() || CategoryViewComponent.hasActiveDrillDowns()) return;

        event.preventDefault();
        this.closeDetail();
    };

    startResize(event: MouseEvent): void {
        event.preventDefault();
        this.#resizeStartY = event.clientY;
        this.#resizeStartHeight = this.detailHeight();
        document.body.style.userSelect = 'none';
        document.body.style.cursor = 'row-resize';
        document.addEventListener('mousemove', this.onResizeMove);
        document.addEventListener('mouseup', this.onResizeEnd);
    }

    private onResizeMove = (event: MouseEvent): void => {
        const delta = this.#resizeStartY - event.clientY;
        this.detailHeight.set(Math.min(Math.max(this.#resizeStartHeight + delta, 80), window.innerHeight - 160));
    };

    private onResizeEnd = (): void => {
        document.removeEventListener('mousemove', this.onResizeMove);
        document.removeEventListener('mouseup', this.onResizeEnd);
        document.body.style.userSelect = '';
        document.body.style.cursor = '';
    };
}
