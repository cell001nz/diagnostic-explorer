import {PropertyBag} from './DiagResponse';
import {customMerge} from '../util/Merge';
import {PropGroup} from './PropGroup';
import {CategoryModel} from './CategoryModel';

export class SubCat {
  cat: CategoryModel;
    name = '';
    groups: PropGroup[] = [];
    isExpanded = true;
    operationSet = '';

    constructor(cat: CategoryModel, bag: PropertyBag) {
      this.cat = cat;
      this.name = bag.name;
      this.update(bag);
    }

    update(bag: PropertyBag) {
        this.operationSet = bag.operationSet;

        this.groups = customMerge(bag.categories,
            this.groups,
            s => s.name,
            t => t.name,
            s => new PropGroup(this, s),
            (s, t) => t.update(s));
    }

  handleDoubleClick(evt: MouseEvent) {
    if (evt.detail === 2)
    {
      this.isExpanded = true;
      this.cat.subCats.forEach(c => c.isExpanded = c === this);
      this.cat.eventSinks.forEach(c => c.isExpanded = false);
    }
  }
}

