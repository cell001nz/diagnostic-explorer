import { EventGridSettings } from './EventGridSettings';

describe('EventGridSettings', () => {
    it('toggles Logger visibility for all event grids', () => {
        const settings = new EventGridSettings();

        expect(settings.showLogger()).toBeTrue();

        settings.toggleLogger();
        expect(settings.showLogger()).toBeFalse();

        settings.toggleLogger();
        expect(settings.showLogger()).toBeTrue();
    });
});
