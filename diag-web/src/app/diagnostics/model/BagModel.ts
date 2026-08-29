﻿import { OperationSet, PropertyBag } from '@domain/DiagResponse';
import { customMerge } from '@util/merge';
import { SubBagModel } from './SubBagModel';
import { PropertySectionModel } from './PropertySectionModel';
import { CategoryModel } from './CategoryModel';
import { computed, signal } from '@angular/core';

export class BagModel {
    cat: CategoryModel;
    name = signal('');
    subBags = signal<SubBagModel[]>([]);
    orderedSubBags = computed(() =>
        [...this.subBags()].sort((left, right) => {
            if (!left.name()) return -1;
            if (!right.name()) return 1;

            return 0;
        })
    );
    propertySections = computed(() => PropertySectionModel.create(this.orderedSubBags()));
    isCollapsed = signal(false);
    isExpanded = computed(() => !this.isCollapsed());
    readonly #sectionExpansionOverrides = signal<ReadonlyMap<string, boolean>>(new Map());

    operationSet = signal('');
    canDrillDown = signal(false);

    constructor(cat: CategoryModel, bag: PropertyBag) {
        this.cat = cat;
        this.name.set(bag.name);
        this.update(bag);
    }

    update(bag: PropertyBag) {
        this.operationSet.set(bag.operationSet);
        this.canDrillDown.set(bag.canDrillDown);

        this.subBags.set(
            customMerge(
                bag.categories,
                this.subBags(),
                (s) => (s.name === 'General' ? '' : (s.name ?? '')),
                (t) => t.name(),
                (s) => new SubBagModel(this, s),
                (s, t) => t.update(s)
            )
        );
    }

    toggleCollapsed() {
        this.isCollapsed.update((v) => !v);
    }

    isSectionCollapsed(section: PropertySectionModel, firstLevelExpanded = true): boolean {
        const defaultExpanded = firstLevelExpanded && section.depth === 1 && section.isExpanded;
        return !(this.#sectionExpansionOverrides().get(section.path) ?? defaultExpanded);
    }

    toggleSection(section: PropertySectionModel): void {
        this.#sectionExpansionOverrides.update((overrides) => {
            const updated = new Map(overrides);
            updated.set(section.path, this.isSectionCollapsed(section));
            return updated;
        });
    }

    getOperationPath(): string {
        return this.cat.name() + '|' + this.name();
    }
    getOperationSet(): OperationSet | null {
        if (!this.operationSet()) return null;

        return this.cat.realtimeModel.getOperationSet(this.operationSet());
    }

    handleDoubleClick(evt: MouseEvent) {
        if (evt.detail === 2) {
            this.isCollapsed.set(false);
            this.cat.bags().forEach((c) => c.isCollapsed.set(c !== this));
            this.cat.eventSinks().forEach((c) => c.isCollapsed.set(true));
        }
    }
}
