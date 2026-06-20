﻿import {OperationSet, PropertyBag} from '@domain/DiagResponse';
import {customMerge} from '@util/merge';
import {SubBagModel} from './SubBagModel';
import {CategoryModel} from './CategoryModel';
import {computed, signal} from "@angular/core";

export class BagModel {
    cat: CategoryModel;
    name = signal('');
    subBags = signal<SubBagModel[]>([]);
    isCollapsed = signal(false);
    isExpanded = computed(() => !this.isCollapsed());
    
    operationSet = signal('');

    constructor(cat: CategoryModel, bag: PropertyBag) {
        this.cat = cat;
        this.name.set(bag.name);
        this.update(bag);
    }

    update(bag: PropertyBag) {
        this.operationSet.set(bag.operationSet);
        
        this.subBags.set(customMerge(bag.categories,
            this.subBags(),
            s => s.name ?? "General",
            t => t.name(),
            s => new SubBagModel(this, s),
            (s, t) => t.update(s)));
    }
    
    toggleCollapsed() {
        this.isCollapsed.update(v => !v);
    }
    
    
    getOperationPath(): string {
        return this.cat.name() + "|" + this.name();
    }
    getOperationSet() : OperationSet | null {
        if (!this.operationSet())
            return null;
        
        return this.cat.realtimeModel.getOperationSet(this.operationSet());
    }

    handleDoubleClick(evt: MouseEvent) {
        if (evt.detail === 2) {
            this.isCollapsed.set(false);
            this.cat.bags().forEach(c => c.isCollapsed.set(c !== this));
            this.cat.eventSinks().forEach(c => c.isCollapsed.set(true));
        }
    }
}

