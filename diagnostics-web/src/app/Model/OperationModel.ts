import {ParameterModel} from './ParameterModel';
import {Operation} from './DiagResponse';

export class OperationModel {
    name = '';
    signature = '';
    parameters: ParameterModel[] = [];


    constructor(operation: Operation) {
        this.name = operation.signature.replace(/\(.*/, '');
        this.signature = operation.signature;
        this.parameters = operation.parameters?.map(p => new ParameterModel(p)) ?? [];

    }
}
