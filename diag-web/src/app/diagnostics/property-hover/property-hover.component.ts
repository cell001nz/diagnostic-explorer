import { ChangeDetectionStrategy, Component, computed, forwardRef, inject, input, OnDestroy, signal } from '@angular/core';
import { DiagProcess } from '@domain/DiagProcess';
import { DiagHubService } from '@services/diag-hub.service';
import { PropModel } from '@model/PropModel';
import { ProcessModel } from '@model/ProcessModel';
import { CategoryViewComponent } from '@app/diagnostics/category-view/category-view.component';
import { tokenizeJson } from '@app/diagnostics/json-tokenizer';

@Component({
    selector: 'app-property-hover',
    imports: [forwardRef(() => CategoryViewComponent)],
    templateUrl: './property-hover.component.html',
    styleUrl: './property-hover.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PropertyHoverComponent implements OnDestroy {
    private static readonly refreshIntervalMilliseconds = 5000;
    prop = input.required<PropModel>();
    process = input.required<DiagProcess>();
    objectPaths = input.required<readonly string[]>();

    readonly processModel = new ProcessModel();
    readonly visible = signal(false);
    readonly loading = signal(false);
    readonly errorMessage = signal('');
    readonly json = signal('');
    readonly isJsonHover = computed(() => this.prop().canJsonHover());
    readonly jsonTokens = computed(() => tokenizeJson(this.json()));

    readonly #hubService = inject(DiagHubService);
    #requestVersion = 0;
    #hideTimer: number | undefined;
    #refreshTimer: number | undefined;
    #loadPending = false;

    show(): void {
        this.cancelHide();
        if (this.visible() || (!this.prop().canJsonHover() && !this.prop().canExpandedHover())) return;

        this.visible.set(true);
        this.loading.set(true);
        this.errorMessage.set('');
        this.json.set('');
        this.startRefreshing();
        this.refresh();
    }

    scheduleHide(): void {
        this.cancelHide();
        this.#hideTimer = window.setTimeout(() => {
            this.visible.set(false);
            this.stopRefreshing();
        }, 150);
    }

    cancelHide(): void {
        if (this.#hideTimer === undefined) return;

        window.clearTimeout(this.#hideTimer);
        this.#hideTimer = undefined;
    }

    ngOnDestroy(): void {
        this.cancelHide();
        this.stopRefreshing();
    }

    private startRefreshing(): void {
        if (this.#refreshTimer !== undefined) return;

        this.#refreshTimer = window.setInterval(() => this.refresh(), PropertyHoverComponent.refreshIntervalMilliseconds);
    }

    private stopRefreshing(): void {
        if (this.#refreshTimer === undefined) return;

        window.clearInterval(this.#refreshTimer);
        this.#refreshTimer = undefined;
    }

    private refresh(): void {
        if (!this.visible() || this.#loadPending) return;

        this.#loadPending = true;
        this.errorMessage.set('');
        const requestVersion = ++this.#requestVersion;
        const objectPaths = [...this.objectPaths(), this.prop().getOperationPath()];
        void this.load(requestVersion, objectPaths);
    }

    private async load(requestVersion: number, objectPaths: string[]): Promise<void> {
        try {
            const response = await this.#hubService.getDrillDown(this.process().id, {
                objectPaths,
                jsonHover: this.isJsonHover(),
                excludeEventViews: true
            });
            if (requestVersion !== this.#requestVersion) return;

            if (response.errorMessage) {
                this.errorMessage.set(response.errorMessage);
                return;
            }

            if (this.isJsonHover()) {
                this.json.set(response.json ?? 'null');
                return;
            }

            this.processModel.setObjectPaths(objectPaths);
            this.processModel.setProcessId(this.process().id, false);
            this.processModel.update(response.diagnostics);
            this.processModel.setDrillDownEventViews([]);
        } catch (error: any) {
            if (requestVersion === this.#requestVersion) this.errorMessage.set(error?.message ?? 'Unable to load property hover.');
        } finally {
            if (requestVersion === this.#requestVersion) this.loading.set(false);
            this.#loadPending = false;
        }
    }
}
