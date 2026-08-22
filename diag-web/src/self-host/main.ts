import { bootstrapApplication } from '@angular/platform-browser';
import { provideZoneChangeDetection } from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';
import { providePrimeNG } from 'primeng/config';
import { MessageService } from 'primeng/api';
import { DiagHubService } from '@services/diag-hub.service';
import { TerminalPreset } from '@app/theme';
import { SelfHostComponent } from './self-host.component';
import { SelfHostDiagHubService } from './self-host-hub.service';

bootstrapApplication(SelfHostComponent, {
    providers: [
        provideZoneChangeDetection(),
        provideAnimations(),
        MessageService,
        providePrimeNG({theme: {preset: TerminalPreset, options: {darkModeSelector: '.app-dark'}}}),
        SelfHostDiagHubService,
        {provide: DiagHubService, useExisting: SelfHostDiagHubService}
    ]
}).catch(error => console.error(error));
