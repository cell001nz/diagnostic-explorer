import {Routes} from '@angular/router';
import {NotAuthorizedComponent} from "@app/pages/not-authorized/not-authorized.component";
import {NotFoundComponent} from "@app/pages/not-found/not-found.component";

export const appRoutes: Routes = [
    { path: '', loadChildren: () => import('./app/members/members.routes')},
    { path: 'notfound', component: NotFoundComponent },
    { path: 'not-authorized', component: NotAuthorizedComponent },
    // { path: '**', redirectTo: '/notfound' }
];
