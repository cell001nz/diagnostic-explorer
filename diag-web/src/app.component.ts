import {Component, DestroyRef, inject, OnInit} from '@angular/core';
import {Router, RouterModule} from '@angular/router';

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [RouterModule],
    template: `<router-outlet></router-outlet>`
})
export class AppComponent {
    
   #router = inject(Router);
   
   constructor() {
   }
   

}
