import { signal } from '@angular/core';
import { CategoryModel } from './CategoryModel';
import { EventModel } from './EventModel';
import { EventSinkModel } from './EventSinkModel';

function createEvent(sequence: number): EventModel {
    return {
        sequence,
        level: 2,
        message: `Event ${sequence}`
    } as EventModel;
}

describe('EventSinkModel', () => {
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
