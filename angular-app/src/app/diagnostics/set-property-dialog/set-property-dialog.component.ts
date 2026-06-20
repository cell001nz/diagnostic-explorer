import {ChangeDetectionStrategy, Component, inject, input, OnInit, signal} from '@angular/core';
import {DynamicDialogRef} from "primeng/dynamicdialog";
import {FormsModule} from "@angular/forms";
import {toObservable} from "@angular/core/rxjs-interop";
import {FloatLabel} from "primeng/floatlabel";
import {InputText} from "primeng/inputtext";
import {ButtonDirective} from "primeng/button";
import {DiagHubService} from "@services/diag-hub.service";
import {OperationResponse, SetPropertyRequest} from "@domain/SetPropertyRequest";

export type SetPropertyStatus = 'idle' | 'executing' | 'success' | 'error' | 'timeout';

@Component({
    selector: 'app-set-property-dialog',
    templateUrl: './set-property-dialog.component.html',
    styleUrls: ['./set-property-dialog.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        FormsModule,
        FloatLabel,
        InputText,
        ButtonDirective
    ]
})
export class SetPropertyDialogComponent implements OnInit {

    text = input.required<string>();
    value = input.required<string>();
    processId = input.required<string>();
    editValue = signal('');
    ref = inject(DynamicDialogRef);
    #hubService = inject(DiagHubService);

    busy = signal(false);
    status = signal<SetPropertyStatus>('idle');
    statusMessage = signal<string>('');
    operationResult = signal<OperationResponse | undefined>(undefined);

    constructor() {
        toObservable(this.value).subscribe(v => this.editValue.set(v));
    }

    ngOnInit(): void {
    }

    onClose(): void {
        this.ref.close();
    }

    async onSetProperty() {
        const request = new SetPropertyRequest();
        request.path = this.text();
        request.value = this.editValue();

        this.busy.set(true);
        this.status.set('executing');
        this.statusMessage.set('Setting property...');
        this.operationResult.set(undefined);

      
        let response: OperationResponse;
        try {
            response = await this.#hubService.setPropertyValue(this.processId(), request);
        } catch (err: any) {
            response = {
                isSuccess: false,
                result: '',
                message: err?.message ?? 'Failed to send set property request',
                detail: ''
            };
        }

        this.busy.set(false);
        this.operationResult.set(response);

        if (response.isSuccess) {
            this.status.set('success');
            this.statusMessage.set('Success');
        } else {
            this.status.set('error');
            this.statusMessage.set('Error');
        }
    }

    handleKeyUp(evt: KeyboardEvent) {
        if (evt.key === 'Enter' && !this.busy())
            this.onSetProperty();

        if (evt.key === 'Escape')
            this.onClose();
    }

    truncateMessage(message: string): string {
        const newlineIdx = message.indexOf('\n');
        const limit = newlineIdx >= 0 ? Math.min(newlineIdx, 200) : 200;
        const truncated = message.substring(0, limit);
        return truncated.length < message.length ? truncated + '…' : truncated;
    }


}
