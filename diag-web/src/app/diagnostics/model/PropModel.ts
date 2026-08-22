import {OperationSet, Property} from '@domain/DiagResponse';
import {SubBagModel} from './SubBagModel';
import {signal} from "@angular/core";

export class PropModel {
    subBag: SubBagModel;
    name = signal('');
    value = signal('');
    description = signal('');
    operationSet = signal('');
    canSet = signal(false);

    constructor(subBag: SubBagModel, source: Property) {
        this.subBag = subBag;
        this.name.set(source.name);
        this.update(source);
    }

    update(source: Property): void {
        this.value.set(source.value);
        this.description.set(source.description);
        this.operationSet.set(source.operationSet);
        this.canSet.set(source.canSet);
    }

    
    getOperationSet() : OperationSet | null {
        if (!this.operationSet())
            return null;
        
        return this.subBag.bag.cat.realtimeModel.getOperationSet(this.operationSet());
    }

    getOperationPath(): string {
        return this.subBag.getOperationPath() + "|" + this.name();
    }
}
