import {SystemEvent} from './DiagResponse';
import {IFilterableEvent} from './IFilterableEvent';

export class DiagnosticMsg extends SystemEvent implements IFilterableEvent {
    machine = '';
    process = '';
    user = '';
    isSelected = false;
    msgId = '';
}
