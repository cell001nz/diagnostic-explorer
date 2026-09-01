import { ChangeDetectionStrategy, Component, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { CategoryViewComponent } from '@app/diagnostics/category-view/category-view.component';
import { EventDetailPanelComponent } from '@app/diagnostics/event-detail-panel/event-detail-panel.component';
import { EventModel } from '@model/EventModel';
import { ProcessModel } from '@model/ProcessModel';
import { Slider } from 'primeng/slider';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { SelfHostDiagHubService } from './self-host-hub.service';
import { SelfHostNavigationService } from './self-host-navigation.service';

const LOCAL_PROCESS_ID = 'self';

@Component({
    selector: 'app-self-host',
    imports: [Tabs, TabPanel, TabList, Tab, TabPanels, CategoryViewComponent, EventDetailPanelComponent, FormsModule, Slider],
    templateUrl: './self-host.component.html',
    styleUrl: './self-host.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SelfHostComponent implements OnInit, OnDestroy {
    readonly hub = inject(SelfHostDiagHubService);
    readonly #navigation = inject(SelfHostNavigationService);
    readonly connectionState = this.hub.connectionState;
    readonly error = this.hub.error;
    readonly process = this.hub.process;
    readonly diagnosticsRefreshIntervalSeconds = this.hub.diagnosticsRefreshIntervalSeconds;
    readonly processModel = signal(new ProcessModel());
    readonly selectedEvent = signal<EventModel | null>(null);
    readonly detailHeight = signal(200);
    #resizeStartY = 0;
    #resizeStartHeight = 0;

    constructor() {
        this.processModel().setProcessId(LOCAL_PROCESS_ID);
        this.hub.logStreamInitialized$.pipe(takeUntilDestroyed()).subscribe((data) => this.processModel().initializeLogStream(data.initialization));
        this.hub.logStreamEvents$.pipe(takeUntilDestroyed()).subscribe((data) => this.processModel().appendLogStreamEvents(data.events));
        this.hub.connectionReconnected$.pipe(takeUntilDestroyed()).subscribe(() => this.processModel().resetEventStream());
        this.hub.diagsArrived$.pipe(takeUntilDestroyed()).subscribe((data) => this.processModel().update(data.response));

        this.#navigation.initialize();
        effect(() => this.syncNavigation());
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
        this.#navigation.destroy();
        void this.hub.stop();
    }

    async refresh(): Promise<void> {
        await this.hub.subscribeProcess(LOCAL_PROCESS_ID);
    }

    async setDiagnosticsRefreshInterval(seconds: number): Promise<void> {
        try {
            await this.hub.setDiagnosticsRefreshInterval(seconds);
        } catch {}
    }

    onCategoryChange(category: string | number | undefined): void {
        if (category == null) return;

        const name = String(category);
        this.processModel().activeCatName.set(name);
        if (this.#navigation.state().category === name) return;

        this.#navigation.selectCategory(name);
    }

    expandCollapse(): void {
        this.processModel().activeCat()?.expandCollapse();
    }

    onEventSelected(event: EventModel): void {
        const previous = this.selectedEvent();
        if (previous && previous !== event) previous.isSelected = false;
        event.isSelected = true;
        this.selectedEvent.set(event);
    }

    closeDetail(): void {
        const selected = this.selectedEvent();
        if (selected) selected.isSelected = false;
        this.selectedEvent.set(null);
    }

    private syncNavigation(): void {
        const model = this.processModel();
        const categories = model.categories();
        if (!categories.length) return;

        const requestedCategory = this.#navigation.state().category;
        if (requestedCategory && categories.some((category) => category.name() === requestedCategory)) {
            model.activeCatName.set(requestedCategory);
            return;
        }

        const activeCategory = model.activeCatName();
        if (activeCategory) this.#navigation.replaceCategory(activeCategory, !!requestedCategory);
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
