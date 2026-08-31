import { LogStreamEvent, LogStreamInitialization } from '@domain/DiagResponse';
import { CategoryModel } from './CategoryModel';
import { EventSinkModel } from './EventSinkModel';
import { ProcessEventStore } from './ProcessEventStore';

function createEvent(sequence: number, loggerCategory = 'Sample.Widget'): LogStreamEvent {
    return {
        streamId: 'stream-1',
        sequence,
        timestampUtc: new Date(2026, 7, 31, 12, 0, sequence),
        loggerCategory,
        level: 2,
        message: `Event ${sequence}`,
        detail: '',
        eventId: 1
    };
}

function createInitialization(maxEvents = 5_000): LogStreamInitialization {
    return {
        streamId: 'stream-1',
        routing: {
            matchMode: 'AllMatches',
            routes: [
                {
                    order: 0,
                    loggerName: 'Sample',
                    loggerNameMatchMode: 'Prefix',
                    stopProcessing: false,
                    destinations: [
                        {
                            category: { source: 'Fixed', value: 'Application' },
                            name: { source: 'Fixed', value: 'Events' }
                        }
                    ]
                }
            ]
        },
        replayEvents: [],
        highWatermark: 0,
        maxEvents
    };
}

describe('ProcessEventStore', () => {
    it('maintains a destination index for routed events without duplicating replayed frames', () => {
        const store = new ProcessEventStore();
        store.initialize(createInitialization());

        const received = store.append([createEvent(1), createEvent(2, 'Other.Logger')]);
        const replayed = store.append([createEvent(1)]);

        expect(received.map((event) => event.sequence)).toEqual([1, 2]);
        expect(replayed.map((event) => event.sequence)).toEqual([1]);
        expect(store.eventsForDestination('Application', 'Events').map((event) => event.sequence)).toEqual([1]);
    });

    it('removes evicted events from their destination index', () => {
        const store = new ProcessEventStore();
        store.initialize(createInitialization(2));

        store.append([createEvent(1), createEvent(2), createEvent(3)]);

        expect(store.events().map((event) => event.sequence)).toEqual([3, 2]);
        expect(store.eventsForDestination('Application', 'Events').map((event) => event.sequence)).toEqual([3, 2]);
    });

    it('updates a cached event-grid view when a routed event arrives', () => {
        const store = new ProcessEventStore();
        store.initialize(createInitialization());
        const sink = new EventSinkModel({} as CategoryModel, 'Events', () => store.eventsForDestination('Application', 'Events'));

        expect(sink.filteredEvents()).toEqual([]);
        store.append([createEvent(1)]);

        expect(sink.filteredEvents().map((event) => event.sequence)).toEqual([1]);
    });
});
