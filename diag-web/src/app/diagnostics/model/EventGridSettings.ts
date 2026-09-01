import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class EventGridSettings {
    showLogger = signal(true);

    toggleLogger(): void {
        this.showLogger.update((visible) => !visible);
    }
}
