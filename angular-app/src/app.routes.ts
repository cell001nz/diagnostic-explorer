import {Routes} from '@angular/router';

export const appRoutes: Routes = [
    { path: '', loadChildren: () => import('./app/members/members.routes')},
    // { path: '**', redirectTo: '/notfound' }
];
