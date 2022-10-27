import {OperationParameter} from './DiagResponse';

export class ParameterModel {
    name = '';
    type = '';
    value = '';

    constructor(p: OperationParameter) {
        this.name = p.name;
        this.type = p.type;
        this.value = '';
    }
}
