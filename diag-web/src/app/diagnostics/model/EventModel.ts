import {IFilterableEvent} from './IFilterableEvent';
import {ScopeNode} from "./ScopeNode";
import {LogStreamEvent, SystemEvent} from "@domain/DiagResponse";

export class EventModel implements SystemEvent, IFilterableEvent {
    isSelected = false;
    machine = '';
    process = '';
    user = '';
    id: number;
    sinkSeq: number;
    date: string | Date;
    message: string;
    detail: string;
    level: number;
    sinkName = '';
    sinkCategory = '';
    streamId = '';
    sequence = 0;
    loggerCategory = '';
    eventId = 0;
    eventName = '';

    region?: ScopeNode;

    constructor(evt: SystemEvent | LogStreamEvent) {

        if ('sequence' in evt) {
            this.streamId = evt.streamId;
            this.sequence = evt.sequence;
            this.loggerCategory = evt.loggerCategory;
            this.eventId = evt.eventId;
            this.eventName = evt.eventName ?? '';
            this.id = evt.sequence;
            this.sinkSeq = evt.sequence;
            this.date = evt.timestampUtc;
            this.level = evt.level;
            this.message = evt.message;
            this.detail = evt.detail;
            this.region = ScopeNode.parseTraceScope(this.displayText);
            return;
        }

        this.id = evt.id;
        this.sinkSeq = evt.sinkSeq;
        this.date = evt.date;
        this.level = evt.level;
        this.message = evt.message;
        this.detail = evt.detail;
        this.sinkName = evt.sinkName;
        this.sinkCategory = evt.sinkCategory;

        this.region = ScopeNode.parseTraceScope(this.displayText);
    }


    get displayText(): string {
        return this.detail ?? this.message;
    }

}
