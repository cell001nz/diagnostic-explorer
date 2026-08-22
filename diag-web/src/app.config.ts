import {HTTP_INTERCEPTORS, provideHttpClient, withFetch} from '@angular/common/http';
import {ApplicationConfig, inject, Injector, provideAppInitializer} from '@angular/core';
import {
    provideRouter,
    withComponentInputBinding,
    withEnabledBlockingInitialNavigation,
    withInMemoryScrolling,
    withRouterConfig
} from '@angular/router';
import Aura from '@primeng/themes/aura';
import { definePreset } from '@primeng/themes';
import { providePrimeNG } from 'primeng/config';
import { appRoutes } from './app.routes';
import {MessageService} from "primeng/api";
import {provideAnimations} from "@angular/platform-browser/animations";

// Terminal / amber-green phosphor vibe: neon-lime primary ramp on a near-black surface.
const TerminalPreset = definePreset(Aura, {
    semantic: {
        primary: {
            50: '#f7fee7',
            100: '#ecfccb',
            200: '#d9f99d',
            300: '#bef264',
            400: '#a3e635',
            500: '#84cc16',
            600: '#65a30d',
            700: '#4d7c0f',
            800: '#3f6212',
            900: '#365314',
            950: '#1a2e05'
        },
        colorScheme: {
            dark: {
                primary: {
                    color: '#a3e635',
                    contrastColor: '#0a0f02',
                    hoverColor: '#bef264',
                    activeColor: '#84cc16'
                }
            }
        }
    }
});

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
