import { Component } from '@angular/core';
import { Divider } from "primeng/divider";
import { RouterModule } from "@angular/router";
import { RetroNavComponent } from "../retro-nav/retro-nav.component";
import { RetroListComponent } from "../retro-list/retro-list.component";

@Component({
  imports: [Divider, RouterModule, RetroNavComponent, RetroListComponent],
  templateUrl: './retro-main.component.html',
  styleUrl: './retro-main.component.scss',
})
export class RetroMainComponent {

}
