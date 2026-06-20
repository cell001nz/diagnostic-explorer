import { Component } from '@angular/core';
import {Button} from "primeng/button";
import {RouterLink} from "@angular/router";

@Component({
  selector: 'app-not-authorized',
  imports: [
    Button,
    RouterLink
  ],
  templateUrl: './not-authorized.component.html',
  styleUrl: './not-authorized.component.scss'
})
export class NotAuthorizedComponent {

}
