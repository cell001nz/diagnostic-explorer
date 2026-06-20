import {ChangeDetectionStrategy, Component, input, output} from '@angular/core';
import {DatePipe} from '@angular/common';
import {LevelToStringPipe} from '@app/pipes/level-to-string.pipe';
import {EventModel} from '@model/EventModel';

@Component({
    selector: 'app-event-detail-panel',
    imports: [
        DatePipe,
        LevelToStringPipe,
    ],
    templateUrl: './event-detail-panel.component.html',
    styleUrl: './event-detail-panel.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventDetailPanelComponent {
    event = input.required<EventModel>();
    closed = output<void>();
}

