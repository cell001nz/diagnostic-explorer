import { PropModel } from './PropModel';
import { OperationSet, SubBag } from '@domain/DiagResponse';
import { customMerge } from '@util/merge';
import { BagModel } from './BagModel';
import { computed, signal } from '@angular/core';

export class SubBagModel {
    bag: BagModel;
    readonly name = signal('');
    readonly operationSet = signal('');
    readonly canDrillDown = signal(false);

    properties = signal<PropModel[]>([]);
    alertSeverity = computed(() => Math.max(0, ...this.properties().map((property) => property.alertSeverity())));
    alertTooltip = computed(() =>
        this.properties()
            .filter((property) => property.alertSeverity())
            .map((property) => `${property.name()}: ${property.alertTooltip()}`)
            .join('\n')
    );

    constructor(subBag: BagModel, propCat: SubBag) {
        this.bag = subBag;
        this.name.set(propCat.name);
        this.operationSet.set(propCat.operationSet);
        this.update(propCat);
    }

    getOperationSet(): OperationSet | null {
        if (!this.operationSet()) return null;

        return this.bag.cat.realtimeModel.getOperationSet(this.operationSet());
    }

    getOperationPath(): string {
        return this.bag.getOperationPath() + '|' + this.name();
    }

    update(propCat: SubBag) {
        this.canDrillDown.set(propCat.canDrillDown);
        this.properties.set(
            customMerge(
                propCat.properties,
                this.properties(),
                (s) => s.name,
                (t) => t.name(),
                (s) => new PropModel(this, s),
                (s, t) => t.update(s)
            )
        );
    }
}
