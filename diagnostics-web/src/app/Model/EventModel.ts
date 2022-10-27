import {SystemEvent} from './DiagResponse';
import {IFilterableEvent} from './IFilterableEvent';
import {Level} from './Level';

export class EventModel extends SystemEvent implements IFilterableEvent {
  isSelected = false;
  machine = '';
  process = '';
  user = '';

  constructor(evt: SystemEvent) {
    super();

    this.id = evt.id;
    this.date = evt.date;
    this.level = evt.level;
    this.message = evt.message;
    this.detail = evt.detail;
  }


  get displayText(): string {
    return this.detail ?? this.message;
  }

}

