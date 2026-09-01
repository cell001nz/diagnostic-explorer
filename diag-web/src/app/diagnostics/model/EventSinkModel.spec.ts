import { signal } from '@angular/core';
import { CategoryModel } from './CategoryModel';
import { EventModel } from './EventModel';
import { EventSinkModel } from './EventSinkModel';

function createEvent(sequence: number, level = 2): EventModel {
    return {
        sequence,
        level,
        message: `Event ${sequence}`
    } as EventModel;
}

describe('EventSinkModel', () => {
    it('filters events to an inclusive level range', () => {
        const events = signal([createEvent(1, 1), createEvent(2, 2), createEvent(3, 3), createEvent(4, 4)]);
        const sink = new EventSinkModel({} as CategoryModel, 'Events', events);

        sink.setLevelRange([2, 3]);

        expect(sink.filteredEvents().map((event) => event.sequence)).toEqual([2, 3]);
    });

    it('normalizes a reversed level range', () => {
        const sink = new EventSinkModel({} as CategoryModel, 'Events');

        sink.levelRange.set([5, 1]);

        expect(sink.normalizedLevelRange).toEqual([1, 5]);
    });

    it('assigns stable event numbers in arrival order while displaying newest events first', () => {
        const firstEvent = createEvent(1);
        const secondEvent = createEvent(2);
        const eventSource = signal([secondEvent, firstEvent]);
        const sink = new EventSinkModel({} as CategoryModel, 'Events', eventSource, () => true);
        const thirdEvent = createEvent(3);

        eventSource.set([thirdEvent, secondEvent, firstEvent]);
        sink.recordAddedEvents([thirdEvent]);

        expect(sink.events().map((event) => sink.getEventNumber(event))).toEqual([3, 2, 1]);
    });

    it('freezes displayed events while paused but continues recording the event rate', () => {
        const eventSource = signal([createEvent(1)]);
        const sink = new EventSinkModel({} as CategoryModel, 'Events', eventSource, () => true);
        const secondEvent = createEvent(2);

        sink.recordAddedEvents(eventSource(), 1_000);
        sink.togglePaused();
        eventSource.set([...eventSource(), secondEvent]);
        sink.recordAddedEvents([secondEvent], 1_100);

        expect(sink.events().map((event) => event.sequence)).toEqual([1]);
        expect(sink.queuedEventCount()).toBe(1);
        expect(sink.getEventsPerSecond(1_100)).toBeCloseTo(2 / 3);

        sink.togglePaused();

        expect(sink.events().map((event) => event.sequence)).toEqual([1, 2]);
        expect(sink.queuedEventCount()).toBe(0);
    });
});
