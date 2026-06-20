import {Routes} from '@angular/router';
import {AppLayout} from '@app/app-layout/app.layout';
import {DiagnosticsMainComponent} from '@app/diagnostics/diagnostics-main/diagnostics-main.component';
import {DiagnosticsViewComponent} from '@app/diagnostics/diagnostics-view/diagnostics-view.component';
import {SelectProcessComponent} from '@app/diagnostics/select-process/select-process.component';
import {RetroMainComponent} from '@app/diagnostics/retro/retro-main/retro-main.component';

export const appRoutes: Routes = [
    {
        path: '', component: AppLayout, canActivate: [], children: [
            {path: '', pathMatch: 'full', redirectTo: 'realtime'},
            {
                path: 'realtime', component: DiagnosticsMainComponent, children: [
                    {path: '', component: SelectProcessComponent},
                    {path: ':processId', component: DiagnosticsViewComponent},
                ]
            },
            {path: 'retro', component: RetroMainComponent},
        ]
    },
    // { path: '**', redirectTo: '/notfound' }
];
