import {SystemEvent} from './DiagResponse';
import {IFilterableEvent} from './IFilterableEvent';

export class DiagnosticMsg extends SystemEvent implements IFilterableEvent {
  level = 0;
  id = 0;
  machine = '';
  message = '';
  process = '';
  user = '';
  isSelected = false;
  msgId = '';
}
