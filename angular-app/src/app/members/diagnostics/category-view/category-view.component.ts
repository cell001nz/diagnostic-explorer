import {ChangeDetectionStrategy, Component, inject, input, output} from '@angular/core';
import {CategoryModel} from "@model/CategoryModel";
import {Panel} from "primeng/panel";
import {PanelModule} from 'primeng/panel';
import {Fieldset} from "primeng/fieldset";
import {PropModel} from "@model/PropModel";
import {DialogService} from "primeng/dynamicdialog";
import {SetPropertyDialogComponent} from "@app/members/diagnostics/set-property-dialog/set-property-dialog.component";
import {OperationsDialogComponent} from "@app/members/diagnostics/operations-dialog/operations-dialog.component";
import {DiagProcess} from "@domain/DiagProcess";
import {OperationSet} from "@domain/DiagResponse";
import {EventSinkViewComponent} from "@app/members/diagnostics/event-sink-view/event-sink-view.component";
import {EventModel} from "@model/EventModel";

@Component({
    selector: 'app-category-view',
    imports: [
        Panel,
        PanelModule,
        Fieldset,
        EventSinkViewComponent,
    ],
    templateUrl: './category-view.component.html',
    styleUrl: './category-view.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [DialogService]
})
export class CategoryViewComponent {

    category = input.required<CategoryModel>();
    process = input.required<DiagProcess>();
    dialogService = inject(DialogService);
    eventSelected = output<EventModel>();

    showSetPropertyDialog(prop: PropModel): void {
        if (!prop.canSet())
            return;

        this.dialogService.open(SetPropertyDialogComponent, {
            showHeader: false,
            maximizable: false,
            width: '500px',
            inputValues: {
                text: prop.getOperationPath(),
                value: prop.value(),
                processId: this.process().id
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
                processId: this.process().id
            }
        });
    }
}
