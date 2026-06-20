import { Routes } from '@angular/router';
import {NotAuthorizedComponent} from "../pages/not-authorized/not-authorized.component";
import {AppLayout} from "./app-layout/app.layout";
import {DiagnosticsMainComponent} from "@app/members/diagnostics/diagnostics-main/diagnostics-main.component";
import {NotFoundComponent} from "@app/pages/not-found/not-found.component";
import {DiagnosticsViewComponent} from "@app/members/diagnostics/diagnostics-view/diagnostics-view.component";
import {SelectProcessComponent} from "@app/members/diagnostics/select-process/select-process.component";
import { RetroMainComponent } from '@app/members/diagnostics/retro/retro-main/retro-main.component';

export default [
    {path: '', component: AppLayout, canActivate: [], children: [

            { path: '', pathMatch: 'full', redirectTo: 'realtime' },
            { path: 'realtime', component: DiagnosticsMainComponent, children: [
                    { path: '', component: SelectProcessComponent},
                    { path: ':processId', component: DiagnosticsViewComponent},

                ]
            },
            { path: 'retro', component: RetroMainComponent },

            /*  
                { path: 'sites', children: [
                        { path: '', component: SitesComponent },
                        { path: ':id', component: EditSiteComponent, data: { editMode: true } },
                        { path: 'new', component: EditSiteComponent, data: { editMode: false }  },
                    ] },
            */
        ]}
    ] as Routes;