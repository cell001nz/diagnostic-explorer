import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DiagProcess } from '@domain/DiagProcess';
import { DiagHubService } from '@services/diag-hub.service';
import { ProcessModel } from '@model/ProcessModel';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonDirective } from 'primeng/button';
import { filter } from 'rxjs';
import { CategoryViewComponent } from '@app/diagnostics/category-view/category-view.component';
import { EventDetailPanelComponent } from '@app/diagnostics/event-detail-panel/event-detail-panel.component';
import { EventModel } from '@model/EventModel';

@Component({
    selector: 'app-drill-down-dialog',
    imports: [ButtonDirective, CategoryViewComponent, EventDetailPanelComponent],
    templateUrl: './drill-down-dialog.component.html',
    styleUrl: './drill-down-dialog.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class DrillDownDialogComponent implements OnInit {
    process = input.required<DiagProcess>();
    objectPaths = input.required<readonly string[]>();
    breadcrumbs = input.required<readonly string[]>();

    readonly processModel = new ProcessModel();
    readonly loading = signal(true);
    readonly errorMessage = signal('');
    readonly errorDetail = signal('');
    readonly isTruncated = signal(false);
    readonly displayedCount = signal(0);
    readonly totalCount = signal<number | undefined>(undefined);
    readonly selectedEvent = signal<EventModel | null>(null);
    readonly title = computed(() => this.breadcrumbs().join(' / '));

    readonly #hubService = inject(DiagHubService);
    readonly #destroyRef = inject(DestroyRef);
    readonly #ref = inject(DynamicDialogRef);
    #loadPending = false;
    #resizeStartY = 0;
    #resizeStartHeight = 0;

    ngOnInit(): void {
        this.processModel.setObjectPaths(this.objectPaths());
        this.processModel.setProcessId(this.process().id, false);
        void this.load();

        this.#hubService.logStreamInitialized$
            .pipe(
                filter(({ processId }) => processId === this.process().id),
                takeUntilDestroyed(this.#destroyRef)
            )
            .subscribe(({ initialization }) => this.processModel.initializeLogStream(initialization));

        this.#hubService.logStreamEvents$
            .pipe(
                filter(({ processId }) => processId === this.process().id),
                takeUntilDestroyed(this.#destroyRef)
            )
            .subscribe(({ events }) => this.processModel.appendLogStreamEvents(events));

        this.#hubService.diagsArrived$
            .pipe(
                filter(({ processId }) => processId === this.process().id),
                takeUntilDestroyed(this.#destroyRef)
            )
            .subscribe(() => void this.load());
    }

    async load(): Promise<void> {
        if (this.#loadPending) return;

        this.#loadPending = true;
        this.loading.set(true);
        this.errorMessage.set('');
        this.errorDetail.set('');

        try {
            const response = await this.#hubService.getDrillDown(this.process().id, {
                objectPaths: [...this.objectPaths()]
            });

            if (response.errorMessage) {
                this.errorMessage.set(response.errorMessage);
                this.errorDetail.set(response.errorDetail ?? '');
                return;
            }

            this.processModel.update(response.diagnostics);
            this.processModel.setDrillDownEventViews(response.eventViews);
            this.displayedCount.set(response.displayedCount);
            this.totalCount.set(response.totalCount);
            this.isTruncated.set(response.isTruncated);
        } catch (error: any) {
            this.errorMessage.set(error?.message ?? 'Unable to load drilldown diagnostics.');
        } finally {
            this.loading.set(false);
            this.#loadPending = false;
        }
    }

    close(): void {
        this.#ref.close();
    }

    onEventSelected(event: EventModel): void {
        const previousEvent = this.selectedEvent();
        if (previousEvent && previousEvent !== event) previousEvent.isSelected = false;
        event.isSelected = true;
        this.selectedEvent.set(event);
    }

    closeDetail(): void {
        const previousEvent = this.selectedEvent();
        if (previousEvent) previousEvent.isSelected = false;
        this.selectedEvent.set(null);
    }

    detailHeight = signal(360);

    startResize(event: MouseEvent): void {
        event.preventDefault();
        this.#resizeStartY = event.clientY;
        this.#resizeStartHeight = this.detailHeight();
        document.body.style.userSelect = 'none';
        document.body.style.cursor = 'row-resize';
        document.addEventListener('mousemove', this.#onResizeMove);
        document.addEventListener('mouseup', this.#onResizeEnd);
    }

    readonly #onResizeMove = (event: MouseEvent): void => {
        const delta = this.#resizeStartY - event.clientY;
        const next = Math.min(Math.max(this.#resizeStartHeight + delta, 80), window.innerHeight - 160);
        this.detailHeight.set(next);
    };

    readonly #onResizeEnd = (): void => {
        document.removeEventListener('mousemove', this.#onResizeMove);
        document.removeEventListener('mouseup', this.#onResizeEnd);
        document.body.style.userSelect = '';
        document.body.style.cursor = '';
    };
}
