import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EventDetailPanelComponent } from './event-detail-panel.component';
import { EventModel } from '@model/EventModel';

describe('EventDetailPanelComponent', () => {
    let component: EventDetailPanelComponent;
    let fixture: ComponentFixture<EventDetailPanelComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [EventDetailPanelComponent]
        }).compileComponents();

        fixture = TestBed.createComponent(EventDetailPanelComponent);
        component = fixture.componentInstance;
    });

    it('highlights every valid JSON document within detail text', () => {
        fixture.componentRef.setInput(
            'event',
            new EventModel({
                id: 1,
                sinkSeq: 1,
                date: '2026-08-26T00:00:00Z',
                level: 2,
                message: 'Widget refreshed',
                detail: 'Widget refreshed {\n  "name": "Northstar",\n  "items": [1, 2]\n}\nState.Message: {\n  "name": "Beacon",\n  "enabled": true\n}',
                sinkName: 'Widget',
                sinkCategory: 'Widget'
            })
        );
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelectorAll('.json-key').length).toBe(4);
        expect(fixture.nativeElement.querySelectorAll('.json-literal').length).toBe(1);
        expect(fixture.nativeElement.textContent).toContain('State.Message:');
    });

    it('formats compact JSON in detail text', () => {
        fixture.componentRef.setInput(
            'event',
            new EventModel({
                id: 1,
                sinkSeq: 1,
                date: '2026-08-26T00:00:00Z',
                level: 2,
                message: 'Widget refreshed',
                detail: 'Payload: {"name":"Northstar","items":[1,2]}',
                sinkName: 'Widget',
                sinkCategory: 'Widget'
            })
        );
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('.event-detail-body').textContent).toContain('"name": "Northstar",\n  "items": [\n    1,\n    2\n  ]');
    });

    it('formats a JSON document stored in a structured logging string property', () => {
        fixture.componentRef.setInput(
            'event',
            new EventModel({
                id: 1,
                sinkSeq: 1,
                date: '2026-08-26T00:00:00Z',
                level: 2,
                message: 'Widget refreshed',
                detail: 'Property.Payload: "{\\"section_23_0\\":{\\"value\\":1},\\"section_24_0\\":{\\"value\\":2}}"',
                sinkName: 'Widget',
                sinkCategory: 'Widget'
            })
        );
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('.event-detail-body').textContent).toContain('  },\n  "section_24_0": {');
    });

    it('retains long header messages for responsive ellipsis display and hover', () => {
        const message = 'X'.repeat(101);
        fixture.componentRef.setInput(
            'event',
            new EventModel({
                id: 1,
                sinkSeq: 1,
                date: '2026-08-26T00:00:00Z',
                level: 2,
                message,
                detail: '',
                sinkName: 'Widget',
                sinkCategory: 'Widget'
            })
        );
        fixture.detectChanges();

        const header = fixture.nativeElement.querySelector('.event-detail-message');
        expect(header.textContent).toBe(message);
        expect(header.title).toBe(message);
    });
});
