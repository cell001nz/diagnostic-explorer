import {DiagnosticResponse, OperationSet, PropertyBag, SystemEvent} from '@domain/DiagResponse';
import * as _ from 'lodash-es';
import {computed, inject, Injectable, signal} from '@angular/core';
import {CategoryModel} from './CategoryModel';
import {EventModel} from './EventModel';
import {customMerge} from "@util/merge";
import {ObservableDisposable} from "@model/ObservableDisposable";
import {Subject} from "rxjs";
import {DiagnosticModelFactory} from "@model/DiagnosticModelFactory";
import {strEqCI} from "@util/stringUtil";
import {LoadEventData} from "@domain/SetPropertyRequest";
import {DiagProcess} from "@domain/DiagProcess";

@Injectable({providedIn: 'root'})
export class ProcessModel implements ObservableDisposable {


    selectedEvent?: EventModel;
    titleMessage = signal('');
    categories = signal<CategoryModel[]>([]);
    operationSets = signal<OperationSet[]>([]);
    serverDate = signal<Date | undefined>(undefined);
    activeCatName = signal('');
    activeCat = computed(() => this.categories().find(c => c.name() === this.activeCatName()));

    clear() {
        this.titleMessage.set("...");
        this.categories.set([]);
        this.operationSets.set([]);
        this.serverDate.set(undefined);
        this.activeCatName.set('');
    }
    
    public update(response: DiagnosticResponse) {
        this.titleMessage.set('Received ' + new Date().toISOString().substring(11, 19) + ' UTC');
        this.serverDate.set(new Date(response.serverDate));

        const bagCats: { [key: string]: PropertyBag[] }
            = _.groupBy(response.propertyBags, p => p.category);

        const catData: { name: string, props: PropertyBag[] }[]
            = _.uniq(_.keys(bagCats).concat(this.categories().map(c => c.name())))
            .map(name => ({name, props: bagCats[name] ?? []}));

        let cats = this.categories().slice();

        customMerge(catData,
            cats,
            d => d.name,
            c => c.name(),
            d => new CategoryModel(this, d.name, d.props),
            (d, c) => c.update(d.props),
            false);

        cats = _.sortBy(cats, c => c.name);
            
        if (cats.filter(c => !c.bags().length && !c.eventSinks().length))
            cats = cats.filter(c => c.bags().length || c.eventSinks().length);

        this.categories.set(cats);
        this.operationSets.set(response.operationSets);
        
        if (!this.activeCatName() && this.categories().length)
            this.activeCatName.set(this.categories()[0].name());        
    }
    
    getOperationSet(setName: string | null) : OperationSet | null {
        if (!setName)
            return null;
        
        return this.operationSets().find(s => s.id === setName) ?? null;
    }
    
    clearEvents(): void {
        for (let cat of this.categories())        {
            cat.clearEvents();
        }
    }
    
   loadEvents(data: LoadEventData) {

        // evts.forEach(evt => this.setEventLevel(evt));
        var grouped = _.groupBy<SystemEvent>(data.events, evt => evt.sinkCategory);
        for (const cat in grouped)
            this.getCat(cat).addEvents(grouped[cat]);
    }
    
   streamEvents(evts: SystemEvent[]) {

        // evts.forEach(evt => this.setEventLevel(evt));
        evts.reverse();

        var grouped = _.groupBy<SystemEvent>(evts, evt => evt.sinkCategory);
        for (const cat in grouped)
            this.getCat(cat).addEvents(grouped[cat]);
    }
    
   private getCat(name: string): CategoryModel {
        let cat = this.categories().find(c => strEqCI(c.name(), name));
        if (!cat) {
            cat = new CategoryModel(this, name);
            this.categories.update(val => _.sortBy([...this.categories(), cat!], c => c.name()));
        }

        return cat;
   }

    dispose(): void {}
    disposed$ = new Subject<true>();


}
