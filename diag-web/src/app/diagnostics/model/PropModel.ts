import { OperationSet, Property, PropertyAlert } from '@domain/DiagResponse';
import { SubBagModel } from './SubBagModel';
import { computed, signal } from '@angular/core';

const namedAlertSeverities: Record<string, number> = {
    None: 0,
    Warning: 1,
    Error: 2
};

function getAlertSeverity(alert: PropertyAlert): number {
    return typeof alert.severity === 'number' ? alert.severity : (namedAlertSeverities[alert.severity] ?? 0);
}

export class PropModel {
    subBag: SubBagModel;
    name = signal('');
    value = signal('');
    description = signal('');
    operationSet = signal('');
    canSet = signal(false);
    alerts = signal<PropertyAlert[]>([]);
    alertSeverity = computed(() => Math.max(0, ...this.alerts().map(getAlertSeverity)));
    alertTooltip = computed(() =>
        this.alerts()
            .map((alert) => alert.message)
            .join('\n')
    );

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
        this.alerts.set(source.alerts ?? []);
    }

    getOperationSet(): OperationSet | null {
        if (!this.operationSet()) return null;

        return this.subBag.bag.cat.realtimeModel.getOperationSet(this.operationSet());
    }

    getOperationPath(): string {
        return this.subBag.getOperationPath() + '|' + this.name();
    }
}
