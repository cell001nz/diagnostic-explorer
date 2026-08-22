import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { Operation, OperationParameter, OperationSet } from '@domain/DiagResponse';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { FormsModule } from '@angular/forms';
import { FloatLabel } from 'primeng/floatlabel';
import { InputText } from 'primeng/inputtext';
import { InputNumber } from 'primeng/inputnumber';
import { DatePicker } from 'primeng/datepicker';
import { ButtonDirective } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { DiagHubService } from '@services/diag-hub.service';
import { OperationRequest, OperationResponse } from '@domain/SetPropertyRequest';
import { Subscription } from 'rxjs';
import { getErrorMsg } from '@util/errorUtil';
import { JsonPipe } from '@angular/common';

export type ParamEditorKind = 'string' | 'int' | 'decimal' | 'date' | 'datetime';

const intTypes = new Set(['int', 'long', 'short', 'byte', 'sbyte', 'ushort', 'uint', 'ulong', 'int?', 'long?', 'short?', 'byte?', 'sbyte?', 'ushort?', 'uint?', 'ulong?']);

const decimalTypes = new Set(['decimal', 'double', 'float', 'decimal?', 'double?', 'float?']);

const dateTypes = new Set(['dateonly', 'dateonly?']);
const dateTimeTypes = new Set(['datetime', 'datetime?']);

export function getEditorKind(typeName: string): ParamEditorKind {
    const t = (typeName ?? '').toLowerCase();
    if (intTypes.has(t)) return 'int';
    if (decimalTypes.has(t)) return 'decimal';
    if (dateTypes.has(t)) return 'date';
    if (dateTimeTypes.has(t)) return 'datetime';
    return 'string';
}

export interface OperationParamValue {
    param: OperationParameter;
    value: string;
    numValue: number | null;
    dateValue: Date | null;
    editorKind: ParamEditorKind;
}

export interface OperationViewModel {
    operation: Operation;
    paramValues: OperationParamValue[];
}

export type OperationStatus = 'idle' | 'executing' | 'success' | 'error' | 'timeout';

@Component({
    selector: 'app-operations-dialog',
    imports: [FormsModule, FloatLabel, InputText, InputNumber, DatePicker, ButtonDirective],
    templateUrl: './operations-dialog.component.html',
    styleUrl: './operations-dialog.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class OperationsDialogComponent implements OnInit {
    operationSet = input.required<OperationSet>();
    path = input.required<string>();
    processId = input.required<string>();
    #hubService = inject(DiagHubService);

    ref = inject(DynamicDialogRef);
    messageService = inject(MessageService);
    busy = signal(false);
    result = signal<OperationResponse | undefined>(undefined);

    status = signal<OperationStatus>('idle');
    statusMessage = signal<string>('');

    operationViewModels = signal<OperationViewModel[]>([]);
    selectedOperation = signal<OperationViewModel | null>(null);

    ngOnInit(): void {
        const ops = this.operationSet();
        if (ops?.operations) {
            const vms = ops.operations.map((op) => ({
                operation: op,
                paramValues: (op.parameters ?? []).map((p) => ({
                    param: p,
                    value: '',
                    numValue: null as number | null,
                    dateValue: null as Date | null,
                    editorKind: getEditorKind(p.type)
                }))
            }));
            this.operationViewModels.set(vms);
            if (vms.length > 0) {
                this.selectedOperation.set(vms[0]);
            }
        }
    }

    selectOperation(vm: OperationViewModel): void {
        if (this.busy()) return;
        this.selectedOperation.set(vm);
        this.status.set('idle');
        this.statusMessage.set('');
        this.result.set(undefined);
    }

    getParamDisplayValue(pv: OperationParamValue): string {
        switch (pv.editorKind) {
            case 'int':
            case 'decimal':
                return pv.numValue?.toString() ?? '';
            case 'date':
            case 'datetime':
                return pv.dateValue?.toISOString() ?? '';
            default:
                return pv.value;
        }
    }

    async onExecute(vm: OperationViewModel) {
        const request: OperationRequest = {
            path: this.path(),
            operation: vm.operation.signature,
            arguments: vm.paramValues.map((v) => this.getParamDisplayValue(v))
        };

        this.busy.set(true);
        this.status.set('executing');
        this.result.set(undefined);
        this.statusMessage.set('Executing...');

        let timedOut = false;
        let response: OperationResponse;
        try {
            const timeout = new Promise<never>((_, reject) =>
                setTimeout(() => {
                    timedOut = true;
                    reject(new Error('Operation timed out'));
                }, 10000)
            );
            response = await Promise.race([this.#hubService.executeOperation(this.processId(), request), timeout]);
        } catch (err: any) {
            response = {
                isSuccess: false,
                result: '',
                message: timedOut ? 'Operation timed out after 10 seconds' : (getErrorMsg(err) ?? 'Failed to send set property request'),
                detail: ''
            };
        }

        this.busy.set(false);

        console.log(response);
            this.result.set(response)

        if (response.isSuccess) {
            this.status.set('success');
            this.statusMessage.set('Success');
        } else if (timedOut) {
            this.status.set('timeout');
            this.statusMessage.set('Timed out');
        } else {
            this.status.set('error');
            this.statusMessage.set('Error');
        }
    }

    truncateMessage(message: string): string {
        const newlineIdx = message.indexOf('\n');
        const limit = newlineIdx >= 0 ? Math.min(newlineIdx, 200) : 200;
        const truncated = message.substring(0, limit);
        return truncated.length < message.length ? truncated + '…' : truncated;
    }

    copyResult(result: string): void {
        navigator.clipboard.writeText(result).then(() => {
            this.messageService.add({ severity: 'success', summary: 'Copied', detail: 'Result copied to clipboard', life: 2000 });
        });
    }

    onClose(): void {
        this.ref.close();
    }
}
