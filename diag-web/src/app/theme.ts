import Aura from '@primeng/themes/aura';
import { definePreset } from '@primeng/themes';

export const TerminalPreset = definePreset(Aura, {
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