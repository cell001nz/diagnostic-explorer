import {Component, inject, input, OnDestroy} from '@angular/core';
import {TableModule} from "primeng/table";
import {ErrMsgPipe} from "@pipes/err-msg.pipe";
import {RouterLink} from "@angular/router";
import {DateDiffPipe} from "@pipes/date-diff.pipe";
import {Tooltip} from "primeng/tooltip";
import {ProcessModel} from "@model/ProcessModel";
import {RealtimeModel} from "@model/RealtimeModel";
import { JsonPipe } from '@angular/common';

@Component({
  selector: 'app-diagnostics-nav',
  imports: [
    TableModule,
    RouterLink,
    DateDiffPipe,
    Tooltip
  ],
  templateUrl: './diagnostics-nav.component.html',
  styleUrl: './diagnostics-nav.component.scss'
})
export class DiagnosticsNavComponent {
  #realtimeModel = inject(RealtimeModel);

  
  get processes() { return this.#realtimeModel.filteredProcesses; }
  
}
