import { Component } from '@angular/core';
import {RouterLink} from "@angular/router";
import {Button} from "primeng/button";

@Component({
  selector: 'app-not-found',
  imports: [
    RouterLink,
    Button
  ],
  templateUrl: './not-found.component.html',
  styleUrl: './not-found.component.scss'
})
export class NotFoundComponent {

}
