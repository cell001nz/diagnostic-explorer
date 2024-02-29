import {SystemEvent} from './DiagResponse';
import {IFilterableEvent} from './IFilterableEvent';
import {CollapsibleRegion} from "./CollapsibleRegion";

export class EventModel extends SystemEvent implements IFilterableEvent {
  isSelected = false;
  machine = '';
  process = '';
  user = '';

  regions: CollapsibleRegion[];

  constructor(evt: SystemEvent) {
    super();

    this.id = evt.id;
    this.date = evt.date;
    this.level = evt.level;
    this.message = evt.message;
    this.detail = evt.detail;

    this.regions = CollapsibleRegion.parseRegions(this.displayText);
  }

  get displayText(): string {
    return this.detail ?? this.message;
  }

}
