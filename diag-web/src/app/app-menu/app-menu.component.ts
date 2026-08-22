import {Component, inject} from '@angular/core';
import {RouterLink, RouterLinkActive} from '@angular/router';
import {RealtimeModel} from '@model/RealtimeModel';

@Component({
  selector: 'app-menu',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './app-menu.component.html',
  styleUrl: './app-menu.component.scss'
})
export class AppMenuComponent {
  readonly #realtime = inject(RealtimeModel);

  get realtimeLink(): unknown[] {
    const id = this.#realtime.selectedProcessId();
    return id ? ['realtime', id] : ['realtime'];
  }

  get realtimeQueryParams(): Record<string, string> {
    const cat = this.#realtime.selectedCategory();
    return cat ? {cat} : {};
  }
}
