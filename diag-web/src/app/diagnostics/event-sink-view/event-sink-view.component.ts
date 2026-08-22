import {ChangeDetectionStrategy, Component, input, output} from '@angular/core';
import {DatePipe, LowerCasePipe} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {Panel, PanelModule} from 'primeng/panel';
import {Slider} from 'primeng/slider';
import {InputText} from 'primeng/inputtext';
import {EventSinkModel} from '@model/EventSinkModel';
import {EventModel} from '@model/EventModel';
import {Level} from '@model/Level';
import {LevelToStringPipe} from '@app/pipes/level-to-string.pipe';

@Component({
    selector: 'app-event-sink-view',
    imports: [
        Panel,
        PanelModule,
        DatePipe,
        LevelToStringPipe,
        LowerCasePipe,
        Slider,
        InputText,
        FormsModule,
    ],
    templateUrl: './event-sink-view.component.html',
    styleUrl: './event-sink-view.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventSinkViewComponent {
    sink = input.required<EventSinkModel>();
    eventSelected = output<EventModel>();

    getLevelLabel(value: number): string {
        return Level.LevelToString(value);
    }

    onFilterTextChange(value: string): void {
        this.sink().setFilterText(value);
    }

    onMinLevelChange(value: number): void {
        this.sink().setMinLevel(value);
    }

    selectEvent(event: EventModel): void {
        this.eventSelected.emit(event);
    }

    private mouseDown = false;

    onRowMouseDown(event: EventModel): void {
        this.mouseDown = true;
        this.selectEvent(event);
    }

    onRowMouseOver(event: EventModel): void {
        if (this.mouseDown) {
            this.selectEvent(event);
        }
    }

    onMouseUp(): void {
        this.mouseDown = false;
    }
}

