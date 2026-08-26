import { Property, PropertyAlert } from '@domain/DiagResponse';
import { PropModel } from './PropModel';
import { SubBagModel } from './SubBagModel';

function createProperty(severity: PropertyAlert['severity']): Property {
    return {
        name: 'WidgetCount',
        value: '6',
        description: '',
        operationSet: '',
        canSet: false,
        canDrillDown: true,
        alerts: [{ severity, message: 'Widget count alert', category: 'Widget count' }]
    };
}

describe('PropModel', () => {
    it('normalizes named alert severities', () => {
        const model = new PropModel({} as SubBagModel, createProperty('Warning'));

        expect(model.alertSeverity()).toBe(1);

        model.update(createProperty('Error'));
        expect(model.alertSeverity()).toBe(2);
    });

    it('preserves numeric alert severities', () => {
        const model = new PropModel({} as SubBagModel, createProperty(1));

        expect(model.alertSeverity()).toBe(1);
    });

    it('preserves drilldown capability', () => {
        const model = new PropModel({} as SubBagModel, createProperty('None'));

        expect(model.canDrillDown()).toBeTrue();
    });
});
