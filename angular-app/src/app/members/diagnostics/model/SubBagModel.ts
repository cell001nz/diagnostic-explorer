import {PropModel} from './PropModel';
import {OperationSet, SubBag} from '@domain/DiagResponse';
import {customMerge} from '@util/merge';
import {BagModel} from './BagModel';
import {signal} from "@angular/core";

export class SubBagModel {
    bag: BagModel;
    readonly name = signal('');
    readonly operationSet = signal('');

    properties = signal<PropModel[]>([]);

    constructor(subBag: BagModel, propCat: SubBag) {
        this.bag = subBag;
        this.name.set(propCat.name);
        this.operationSet.set(propCat.operationSet);
        this.update(propCat);
    }

    getOperationSet() : OperationSet | null {
        if (!this.operationSet())
            return null;
        
        return this.bag.cat.realtimeModel.getOperationSet(this.operationSet());
    }
    
    getOperationPath(): string {
        return this.bag.getOperationPath() + "|" + this.name();
    }
    
    update(propCat: SubBag) {
        this.properties.set(customMerge(propCat.properties,
            this.properties(),
            s => s.name,
            t => t.name(),
            s => new PropModel(this, s),
            (s, t) => t.update(s)));
    }
}
