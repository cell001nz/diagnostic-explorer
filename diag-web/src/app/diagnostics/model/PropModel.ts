import { OperationSet, Property, PropertyAlert } from '@domain/DiagResponse';
import { SubBagModel } from './SubBagModel';
import { computed, signal } from '@angular/core';

const namedAlertSeverities: Record<string, number> = {
    None: 0,
    Warning: 1,
    Error: 2
};

type DisplayValueKind = 'null' | 'text' | 'boolean' | 'number' | 'positive-number' | 'zero-number' | 'negative-number' | 'date-time' | 'duration' | 'enumeration' | 'object';

const numericValueKinds: readonly DisplayValueKind[] = ['text', 'null', 'text', 'boolean', 'number', 'positive-number', 'zero-number', 'negative-number', 'date-time', 'duration', 'enumeration', 'object'];

const namedValueKinds: Record<string, DisplayValueKind> = {
    Null: 'null',
    Text: 'text',
    Boolean: 'boolean',
    Number: 'number',
    PositiveNumber: 'positive-number',
    ZeroNumber: 'zero-number',
    NegativeNumber: 'negative-number',
    DateTime: 'date-time',
    Duration: 'duration',
    Enumeration: 'enumeration',
    Object: 'object'
};

function getAlertSeverity(alert: PropertyAlert): number {
    return typeof alert.severity === 'number' ? alert.severity : (namedAlertSeverities[alert.severity] ?? 0);
}

function getValueKind(valueKind: Property['valueKind']): DisplayValueKind {
    if (typeof valueKind === 'number') return numericValueKinds[valueKind] ?? 'object';

    return namedValueKinds[valueKind ?? ''] ?? 'text';
}

export class PropModel {
    subBag: SubBagModel;
    name = signal('');
    value = signal('');
    valueLines = computed(() => this.value().split(/\r?\n/));
    description = signal('');
    operationSet = signal('');
    canSet = signal(false);
    canDrillDown = signal(false);
    drillDownIconOnly = signal(false);
    drillDownText = signal('');
    canJsonHover = signal(false);
    canExpandedHover = signal(false);
    valueKind = signal<DisplayValueKind>('text');
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
        this.value.set(source.value ?? '');
        this.description.set(source.description);
        this.operationSet.set(source.operationSet);
        this.canSet.set(source.canSet);
        this.canDrillDown.set(source.canDrillDown);
        this.drillDownIconOnly.set(source.drillDownIconOnly ?? false);
        this.drillDownText.set(source.drillDownText ?? '');
        this.canJsonHover.set(source.canJsonHover ?? false);
        this.canExpandedHover.set(source.canExpandedHover ?? false);
        this.valueKind.set(getValueKind(source.valueKind));
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
