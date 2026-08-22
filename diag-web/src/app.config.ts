import {HTTP_INTERCEPTORS, provideHttpClient, withFetch} from '@angular/common/http';
import {ApplicationConfig, inject, Injector, provideAppInitializer} from '@angular/core';
import {
    provideRouter,
    withComponentInputBinding,
    withEnabledBlockingInitialNavigation,
    withInMemoryScrolling,
    withRouterConfig
} from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import { appRoutes } from './app.routes';
import {MessageService} from "primeng/api";
import {provideAnimations} from "@angular/platform-browser/animations";
import {TerminalPreset} from './app/theme';

export const appConfig: ApplicationConfig = {
    providers: [
        provideRouter(appRoutes, withComponentInputBinding(),
            withRouterConfig({ paramsInheritanceStrategy: 'always'}),
            withInMemoryScrolling({
            anchorScrolling: 'enabled',
            scrollPositionRestoration: 'enabled'
        }), withEnabledBlockingInitialNavigation()),
        provideHttpClient(withFetch()),
        provideAnimations(),
        MessageService,
        providePrimeNG({theme: {preset: TerminalPreset, options: {darkModeSelector: '.app-dark'}}}),
         provideAppInitializer(async () => {
            })           
    ]
};
