import { PropertyBag, SubBag } from '@domain/DiagResponse';
import { BagModel } from './BagModel';
import { CategoryModel } from './CategoryModel';

function createSubBag(name: string | null): SubBag {
    return {
        name,
        operationSet: '',
        canDrillDown: false,
        properties: []
    };
}

function createBag(categories: SubBag[]): PropertyBag {
    return {
        name: 'Widget',
        category: 'Status',
        operationSet: '',
        canDrillDown: false,
        categories
    };
}

describe('BagModel', () => {
    it('places the unnamed sub-bag before all named sub-bags', () => {
        const model = new BagModel({} as CategoryModel, createBag([createSubBag('Connection'), createSubBag(null), createSubBag('Advanced')]));

        expect(model.orderedSubBags().map((subBag) => subBag.name())).toEqual([null, 'Connection', 'Advanced']);
    });
});
