import {Component, inject, input, OnDestroy} from '@angular/core';
import {DiagnosticsNavComponent} from "@app/diagnostics/diagnostics-nav/diagnostics-nav.component";
import {RouterOutlet} from "@angular/router";
import {Divider} from "primeng/divider";

@Component({
  selector: 'app-diagnostics-main',
  imports: [
    DiagnosticsNavComponent,
    RouterOutlet,
    Divider
  ],
  templateUrl: './diagnostics-main.component.html',
  styleUrl: './diagnostics-main.component.scss'
})
export class DiagnosticsMainComponent {
  
  processId = input<number, string>(0, { transform: (value: string) => Number(value) });

  constructor() {
  }

}
