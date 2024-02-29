import {APP_INITIALIZER, NgModule} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {MatTabsModule} from "@angular/material/tabs";
import {MatSidenavModule} from "@angular/material/sidenav";
import {BrowserAnimationsModule} from "@angular/platform-browser/animations";
import {MatToolbarModule} from "@angular/material/toolbar";
import {RetroNavComponent} from './retro-nav/retro-nav.component';
import {RetroDisplayComponent} from './retro-display/retro-display.component';
import {RealtimeNavComponent} from './realtime-nav/realtime-nav.component';
import {RealtimeDisplayComponent} from './realtime-display/realtime-display.component';
import {MatIconModule} from "@angular/material/icon";
import {MatButtonModule} from "@angular/material/button";
import {HttpClientModule} from '@angular/common/http';
import {MatTableModule} from '@angular/material/table';
import {MatInputModule} from '@angular/material/input';
import {MatListModule} from '@angular/material/list';
import {FormsModule} from '@angular/forms';
import { RealtimeCategoryComponent } from './realtime-category/realtime-category.component';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatCardModule} from '@angular/material/card';
import { RealtimeEventsComponent } from './realtime-events/realtime-events.component';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatSnackBarModule} from '@angular/material/snack-bar';
import { EventFilterComponent } from './event-filter/event-filter.component';
import {MatCheckboxModule} from '@angular/material/checkbox';
import { SetPropertyDialogComponent } from './set-property-dialog/set-property-dialog.component';
import {MatDialogModule} from '@angular/material/dialog';
import { InfoDialogComponent } from './info-dialog/info-dialog.component';
import { ExecOperationsComponent } from './exec-operations/exec-operations.component';
import {MatMenuModule} from '@angular/material/menu';
import {MatSelectModule} from '@angular/material/select';
import {MatDatepickerModule} from '@angular/material/datepicker';
import {MAT_DATE_LOCALE, MatNativeDateModule} from '@angular/material/core';
import {DatePipe} from '@angular/common';
import { SummaryLinePipe } from './pipes/summary-line.pipe';
import {MatProgressBarModule} from '@angular/material/progress-bar';
import { LevelNamePipe } from './pipes/level-name.pipe';
import {AngularSplitModule} from 'angular-split';
import {AppConfigService} from './services/app-config.service';
import {CollapsibleRegionComponent} from "./collapsible-region/collapsible-region.component";

@NgModule({
  declarations: [
    AppComponent,
    RetroNavComponent,
    RetroDisplayComponent,
    RealtimeNavComponent,
    RealtimeDisplayComponent,
    RealtimeCategoryComponent,
    RealtimeEventsComponent,
    EventFilterComponent,
    SetPropertyDialogComponent,
    InfoDialogComponent,
    ExecOperationsComponent,
    SummaryLinePipe,
    LevelNamePipe,
    CollapsibleRegionComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    MatDialogModule,
    MatTabsModule,
    MatSidenavModule,
    BrowserAnimationsModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatSnackBarModule,
    HttpClientModule,
    MatTableModule,
    MatInputModule,
    FormsModule,
    MatExpansionModule,
    MatCardModule,
    MatTooltipModule,
    MatCheckboxModule,
    MatMenuModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressBarModule,
    AngularSplitModule,
    MatListModule
  ],
  providers: [
    {provide: MAT_DATE_LOCALE, useValue: 'en-GB'},
    {
      provide: APP_INITIALIZER,
      multi: true,
      deps: [AppConfigService],
      useFactory: (appConfigService: AppConfigService) => () => appConfigService.initialise()
    },
    DatePipe
  ],
  bootstrap: [AppComponent]
})
export class AppModule {
}
