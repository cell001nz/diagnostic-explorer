import {Component, inject} from '@angular/core';
import {TableModule} from "primeng/table";
import {Router, RouterLink, RouterLinkActive} from "@angular/router";
import {DateDiffPipe} from "@pipes/date-diff.pipe";
import {Tooltip} from "primeng/tooltip";
import {ContextMenu} from "primeng/contextmenu";
import {MenuItem} from "primeng/api";
import {RealtimeModel} from "@model/RealtimeModel";
import {RetroModel} from "@model/RetroModel";
import {DiagProcess} from "@domain/DiagProcess";

@Component({
  selector: 'app-diagnostics-nav',
  imports: [
    TableModule,
    RouterLink,
    RouterLinkActive,
    DateDiffPipe,
    Tooltip,
    ContextMenu
  ],
  templateUrl: './diagnostics-nav.component.html',
  styleUrl: './diagnostics-nav.component.scss'
})
export class DiagnosticsNavComponent {
  #realtimeModel = inject(RealtimeModel);
  #retroModel = inject(RetroModel);
  #router = inject(Router);

  selectedProcess?: DiagProcess;

  readonly menuItems: MenuItem[] = [
    {
      label: 'Switch to Retro View',
      icon: 'pi pi-clock-history',
      command: () => this.switchToRetro()
    }
  ];

  get processes() { return this.#realtimeModel.filteredProcesses; }

  onContextMenu(event: MouseEvent, process: DiagProcess, cm: ContextMenu): void {
    this.selectedProcess = process;
    cm.show(event);
  }

  private async switchToRetro(): Promise<void> {
    if (!this.selectedProcess)
      return;

    this.#retroModel.prepareProcessSearch(this.selectedProcess);
    await this.#router.navigate(['/retro']);
    await this.#retroModel.search();
  }
}
