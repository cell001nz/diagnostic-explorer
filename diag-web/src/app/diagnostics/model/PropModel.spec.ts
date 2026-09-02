import { Property, PropertyAlert, PropertyStatus } from '@domain/DiagResponse';
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
        drillDownIconOnly: false,
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

    it('maps named status codes to icons', () => {
        const property = createProperty('None');
        const status: PropertyStatus = { status: 'Paused', text: 'Worker paused' };
        property.statuses = [status];

        const model = new PropModel({} as SubBagModel, property);

        expect(model.statuses()).toEqual([status]);
        expect(model.statusIcon(status)).toContain('bi-pause-circle-fill');
    });

    it('splits values into display lines', () => {
        const property = createProperty('None');
        property.value = 'Widget X\r\nWidget Y';

        const model = new PropModel({} as SubBagModel, property);

        expect(model.valueLines()).toEqual(['Widget X', 'Widget Y']);
    });

    it('tokenizes explicitly configured JSON values', () => {
        const property = createProperty('None');
        property.value = '{"name":"Widget","count":2}';
        property.isJson = true;

        const model = new PropModel({} as SubBagModel, property);

        expect(model.isJson()).toBeTrue();
        expect(model.jsonTokens().some((token) => token.type === 'json-key')).toBeTrue();
        expect(model.jsonTokens().some((token) => token.type === 'json-number')).toBeTrue();
    });
});
