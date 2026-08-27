import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
import { CategoryModel } from '@model/CategoryModel';
import { take } from 'rxjs';
import { Panel } from 'primeng/panel';
import { PanelModule } from 'primeng/panel';
import { Fieldset } from 'primeng/fieldset';
import { PropModel } from '@model/PropModel';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { SetPropertyDialogComponent } from '@app/diagnostics/set-property-dialog/set-property-dialog.component';
import { OperationsDialogComponent } from '@app/diagnostics/operations-dialog/operations-dialog.component';
import { DiagProcess } from '@domain/DiagProcess';
import { OperationSet } from '@domain/DiagResponse';
import { EventSinkViewComponent } from '@app/diagnostics/event-sink-view/event-sink-view.component';
import { EventModel } from '@model/EventModel';
import { PropertyHoverComponent } from '@app/diagnostics/property-hover/property-hover.component';
import { SelfHostNavigationService } from '../../../self-host/self-host-navigation.service';

@Component({
    selector: 'app-category-view',
    imports: [Panel, PanelModule, Fieldset, EventSinkViewComponent, PropertyHoverComponent],
    templateUrl: './category-view.component.html',
    styleUrl: './category-view.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [DialogService]
})
export class CategoryViewComponent {
    private static readonly activeDrillDownLevels = new Set<number>();
    private static readonly activeDrillDowns = new Set<DynamicDialogRef>();
    private static isWatchingForOutsideClick = false;
    category = input.required<CategoryModel>();
    process = input.required<DiagProcess>();
    flatRoot = input(false);
    maxPropertyColumns = input<number>();
    breadcrumbs = input<readonly string[]>([]);
    dialogService = inject(DialogService);
    eventSelected = output<EventModel>();
    readonly #selfHostNavigation = inject(SelfHostNavigationService, { optional: true });
    #activeDrillDown?: { objectPaths: readonly string[]; ref: DynamicDialogRef };

    constructor() {
        if (this.#selfHostNavigation) effect(() => this.syncSelfHostDrillDown());
    }

    static hasActiveDrillDowns(): boolean {
        return CategoryViewComponent.activeDrillDowns.size > 0;
    }

    showSetPropertyDialog(prop: PropModel): void {
        if (!prop.canSet()) return;

        this.dialogService.open(SetPropertyDialogComponent, {
            showHeader: false,
            maximizable: false,
            width: '500px',
            inputValues: {
                text: prop.getOperationPath(),
                value: prop.value(),
                processId: this.process().id,
                objectPaths: this.category().realtimeModel.objectPaths
            }
        });
    }

    showOperationsDialog(opSet: OperationSet, path: string): void {
        this.dialogService.open(OperationsDialogComponent, {
            showHeader: false,
            maximizable: false,
            width: '600px',
            inputValues: {
                operationSet: opSet,
                path: path,
                processId: this.process().id,
                objectPaths: this.category().realtimeModel.objectPaths
            }
        });
    }

    async showDrillDown(path: string, name: string): Promise<void> {
        const { DrillDownDialogComponent } = await import('@app/diagnostics/drill-down-dialog/drill-down-dialog.component');
        const objectPaths = [...this.category().realtimeModel.objectPaths, path];
        const breadcrumbs = [...this.breadcrumbs(), name];
        const cascadeLevel = CategoryViewComponent.reserveDrillDownLevel();
        const cascadeOffset = Math.min(cascadeLevel, 5);

        const ref = this.dialogService.open(DrillDownDialogComponent, {
            showHeader: false,
            maximizable: true,
            modal: true,
            position: 'topleft',
            width: `calc(100vw - min(10em, 10vw) - ${cascadeOffset}rem)`,
            height: `calc(100vh - min(10em, 10vh) - ${cascadeOffset}rem)`,
            style: {
                marginTop: `calc(min(5em, 5vh) + ${cascadeOffset}rem)`,
                marginLeft: `calc(min(5em, 5vw) + ${cascadeOffset}rem)`
            },
            contentStyle: { overflow: 'hidden', height: '100%' },
            inputValues: {
                process: this.process(),
                objectPaths,
                breadcrumbs
            }
        });

        if (ref) {
            this.#activeDrillDown = { objectPaths, ref };
            this.#selfHostNavigation?.drillDownOpened(this.category().name(), objectPaths, breadcrumbs);
            CategoryViewComponent.activeDrillDowns.add(ref);
            CategoryViewComponent.watchForOutsideClick();
            ref.onClose.pipe(take(1)).subscribe(() => {
                if (this.#activeDrillDown?.ref === ref) this.#activeDrillDown = undefined;
                this.#selfHostNavigation?.drillDownClosed(objectPaths);
                CategoryViewComponent.activeDrillDownLevels.delete(cascadeLevel);
                CategoryViewComponent.activeDrillDowns.delete(ref);
                CategoryViewComponent.stopWatchingForOutsideClick();
            });
        } else {
            CategoryViewComponent.activeDrillDownLevels.delete(cascadeLevel);
        }
    }

    private syncSelfHostDrillDown(): void {
        const navigation = this.#selfHostNavigation;
        if (!navigation) return;

        const activeDrillDown = this.#activeDrillDown;
        if (activeDrillDown && !navigation.includesDrillDown(activeDrillDown.objectPaths)) {
            activeDrillDown.ref.close();
            return;
        }

        if (activeDrillDown) return;

        const requested = navigation.nextDrillDown(this.category().name(), this.category().realtimeModel.objectPaths, this.breadcrumbs());
        if (!requested) return;

        const path = requested.objectPaths.at(-1);
        const name = requested.breadcrumbs.at(-1);
        if (path && name) void this.showDrillDown(path, name);
    }

    private static reserveDrillDownLevel(): number {
        let level = 0;
        while (CategoryViewComponent.activeDrillDownLevels.has(level)) level++;

        CategoryViewComponent.activeDrillDownLevels.add(level);
        return level;
    }

    private static dismissTopmostDrillDown(): boolean {
        const topmostDialog = Array.from(CategoryViewComponent.activeDrillDowns).at(-1);
        if (!topmostDialog) return false;

        topmostDialog.close();
        return true;
    }

    private static watchForOutsideClick(): void {
        if (CategoryViewComponent.isWatchingForOutsideClick) return;

        document.addEventListener('pointerdown', CategoryViewComponent.onDocumentPointerDown, true);
        document.addEventListener('keydown', CategoryViewComponent.onDocumentKeyDown, true);
        CategoryViewComponent.isWatchingForOutsideClick = true;
    }

    private static stopWatchingForOutsideClick(): void {
        if (CategoryViewComponent.activeDrillDowns.size || !CategoryViewComponent.isWatchingForOutsideClick) return;

        document.removeEventListener('pointerdown', CategoryViewComponent.onDocumentPointerDown, true);
        document.removeEventListener('keydown', CategoryViewComponent.onDocumentKeyDown, true);
        CategoryViewComponent.isWatchingForOutsideClick = false;
    }

    private static readonly onDocumentPointerDown = (event: PointerEvent): void => {
        if (event.target instanceof Element && event.target.closest('.p-dialog')) return;

        CategoryViewComponent.dismissTopmostDrillDown();
    };

    private static readonly onDocumentKeyDown = (event: KeyboardEvent): void => {
        if (event.key !== 'Escape') return;
        if (!CategoryViewComponent.dismissTopmostDrillDown()) return;

        event.preventDefault();
        event.stopPropagation();
    };
}
