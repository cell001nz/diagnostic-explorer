import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DiagProcess } from '@domain/DiagProcess';
import { DiagHubService } from '@services/diag-hub.service';
import { ProcessModel } from '@model/ProcessModel';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { TabsModule } from 'primeng/tabs';
import { ButtonDirective } from 'primeng/button';
import { filter } from 'rxjs';
import { CategoryViewComponent } from '@app/diagnostics/category-view/category-view.component';

@Component({
    selector: 'app-drill-down-dialog',
    imports: [TabsModule, ButtonDirective, CategoryViewComponent],
    templateUrl: './drill-down-dialog.component.html',
    styleUrl: './drill-down-dialog.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class DrillDownDialogComponent implements OnInit {
    process = input.required<DiagProcess>();
    objectPaths = input.required<readonly string[]>();

    readonly processModel = new ProcessModel();
    readonly loading = signal(true);
    readonly errorMessage = signal('');
    readonly errorDetail = signal('');
    readonly isTruncated = signal(false);
    readonly displayedCount = signal(0);
    readonly totalCount = signal<number | undefined>(undefined);
    readonly title = computed(() => this.processModel.activeCat()?.bags()[0]?.name() ?? '');

    readonly #hubService = inject(DiagHubService);
    readonly #destroyRef = inject(DestroyRef);
    readonly #ref = inject(DynamicDialogRef);
    #loadPending = false;

    ngOnInit(): void {
        this.processModel.setObjectPaths(this.objectPaths());
        void this.load();

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

    onCategoryChange(value: string | number | undefined): void {
        this.processModel.activeCatName.set(value?.toString() ?? '');
    }

    close(): void {
        this.#ref.close();
    }
}
