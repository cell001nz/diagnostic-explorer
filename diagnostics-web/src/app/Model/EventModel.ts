import {SystemEvent} from './DiagResponse';
import {IFilterableEvent} from './IFilterableEvent';
import {ScopeNode} from "./ScopeNode";

export class EventModel extends SystemEvent implements IFilterableEvent {
  isSelected = false;
  machine = '';
  process = '';
  user = '';

  region?: ScopeNode;

  constructor(evt: SystemEvent) {
    super();

    this.id = evt.id;
    this.date = evt.date;
    this.level = evt.level;
    this.message = evt.message;
    this.detail = evt.detail;

    this.region = ScopeNode.parseTraceScope(this.displayText);
  }

  get displayText(): string {
    return this.detail ?? this.message;
  }

}
