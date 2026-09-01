import { EventResponse, PropertyBag, SystemEvent } from '@domain/DiagResponse';
import { customMerge } from '@util/merge';
import { EventSinkModel } from './EventSinkModel';
import { EventModel } from './EventModel';
import { BagModel } from './BagModel';
import { ProcessModel } from './ProcessModel';
import * as _ from 'lodash-es';
import { Level } from './Level';
import { strEqCI } from '@util/stringUtil';
import { Signal, signal, WritableSignal } from '@angular/core';

export class CategoryModel {
    name = signal('');
    // propData = signal<PropertyBag[]>([]);
    eventData: EventResponse[] = [];
    bags = signal<BagModel[]>([]);
    eventSinks = signal<EventSinkModel[]>([]);
    realtimeModel: ProcessModel;
    readonly eventLevels = [
        { values: [Level.Trace, Level.Debug], name: 'verbose' },
        { values: [Level.Information], name: 'info' },
        { values: [Level.Warning], name: 'warn' },
        { values: [Level.Error], name: 'error' },
        { values: [Level.Critical], name: 'critical' }
    ];
    activeEventLevels = signal<ReadonlySet<string>>(new Set());
    private static readonly eventLevelIndicatorDurationMilliseconds = 5_000;
    private readonly eventLevelActivity = new Map<string, number>();
    private readonly eventLevelActivityIds = signal<ReadonlyMap<string, number>>(new Map());

    constructor(
        realtimeModel: ProcessModel,
        name: string,
        props: PropertyBag[] = [],
        private readonly loggerVisible = signal(true)
    ) {
        this.realtimeModel = realtimeModel;
        this.name.set(name);
        if (props) this.update(props);
    }

    update(props: PropertyBag[]) {
        // this.propData.set(props);
        this.bags.set(
            customMerge(
                props,
                this.bags(),
                (s) => s.name,
                (t) => t.name(),
                (s) => new BagModel(this, s),
                (s, t) => t.update(s)
            )
        );
    }

    getSink(name: string, eventProvider: () => EventModel[] = () => [], eventMatcher?: (event: EventModel) => boolean): EventSinkModel {
        let sink = this.eventSinks().find((c) => strEqCI(c.name, name));
        if (!sink) {
            sink = new EventSinkModel(this, name, eventProvider, eventMatcher, this.loggerVisible);
            this.eventSinks.update((sinks) => [...sinks, sink!]);
        }

        return sink;
    }

    expandCollapse(): void {
        console.log('expandCollapse', this.name());
        const expandable: { isCollapsed: WritableSignal<boolean> }[] = [];
        expandable.push(...this.bags());
        expandable.push(...this.eventSinks());

        const allExpanded = expandable.every((item) => !item.isCollapsed());
        expandable.forEach((exp) => exp.isCollapsed.set(allExpanded));
    }

    addEvents(evts: SystemEvent[]) {
        this.recordEventSeverity(evts);

        const grouped = _.groupBy(evts, (evt) => evt.sinkName);
        for (const sinkName in grouped) this.getSink(sinkName).addEvents(grouped[sinkName]);
    }

    recordEventSeverity(evts: readonly { level: number }[], timestamp = Date.now()): void {
        if (evts.length === 0) return;

        const activeLevels = new Set(this.activeEventLevels());
        const activityIds = new Map(this.eventLevelActivityIds());
        for (const event of evts) {
            const level = this.eventLevels.find((candidate) => candidate.values.includes(event.level));
            if (!level) continue;

            this.eventLevelActivity.set(level.name, timestamp);
            activeLevels.add(level.name);
            activityIds.set(level.name, (activityIds.get(level.name) ?? 0) + 1);
        }

        this.activeEventLevels.set(activeLevels);
        this.eventLevelActivityIds.set(activityIds);
    }

    isEventLevelActive(level: string): boolean {
        return this.activeEventLevels().has(level);
    }

    getEventLevelActivityId(level: string): number {
        return this.eventLevelActivityIds().get(level) ?? 0;
    }

    getEventLevelBars() {
        const activityIds = this.eventLevelActivityIds();
        return this.eventLevels.map((level) => ({ ...level, activityId: activityIds.get(level.name) ?? 0 }));
    }

    clearEvents() {
        for (let sink of this.eventSinks()) {
            sink.clearEvents();
        }
    }

    checkEventSeverityLevels(timestamp = Date.now()) {
        const activeLevels = new Set(this.activeEventLevels());
        for (const [level, lastSeen] of this.eventLevelActivity) {
            if (timestamp - lastSeen <= CategoryModel.eventLevelIndicatorDurationMilliseconds) continue;

            this.eventLevelActivity.delete(level);
            activeLevels.delete(level);
        }

        this.activeEventLevels.set(activeLevels);
    }
}
