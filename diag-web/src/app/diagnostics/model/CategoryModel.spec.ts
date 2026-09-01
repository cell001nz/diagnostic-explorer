import { CategoryModel } from './CategoryModel';
import { ProcessModel } from './ProcessModel';

describe('CategoryModel', () => {
    it('groups Trace and Debug into one Verbose bar', () => {
        const category = new CategoryModel({} as ProcessModel, 'Widgets');

        category.recordEventSeverity([{ level: 0 }, { level: 1 }, { level: 2 }, { level: 4 }], 1_000);

        expect(category.eventLevels).toHaveSize(5);
        expect(category.isEventLevelActive('verbose')).toBeTrue();
        expect(category.isEventLevelActive('info')).toBeTrue();
        expect(category.isEventLevelActive('error')).toBeTrue();
        expect(category.isEventLevelActive('critical')).toBeFalse();
    });

    it('removes a level bar after its activity expires', () => {
        const category = new CategoryModel({} as ProcessModel, 'Widgets');

        category.recordEventSeverity([{ level: 3 }], 1_000);
        category.checkEventSeverityLevels(6_001);

        expect(category.isEventLevelActive('warn')).toBeFalse();
    });

    it('refreshes activity for a level that receives another event', () => {
        const category = new CategoryModel({} as ProcessModel, 'Widgets');

        category.recordEventSeverity([{ level: 2 }], 1_000);
        const firstActivityId = category.getEventLevelActivityId('info');
        category.recordEventSeverity([{ level: 2 }], 4_000);

        expect(category.getEventLevelActivityId('info')).toBeGreaterThan(firstActivityId);
        category.checkEventSeverityLevels(6_001);
        expect(category.isEventLevelActive('info')).toBeTrue();
    });
});
