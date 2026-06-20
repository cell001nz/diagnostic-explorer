﻿export class Level {
    // Microsoft.Extensions.Logging.LogLevel ordinals
    public static TRACE = 0;
    public static DEBUG = 1;
    public static INFORMATION = 2;
    public static WARNING = 3;
    public static ERROR = 4;
    public static CRITICAL = 5;
    public static NONE = 6;

    public static LevelToString(value: number): string {
        switch (value) {
            case Level.TRACE: return 'Trace';
            case Level.DEBUG: return 'Debug';
            case Level.INFORMATION: return 'Info';
            case Level.WARNING: return 'Warn';
            case Level.ERROR: return 'Error';
            case Level.CRITICAL: return 'Critical';
            case Level.NONE: return 'None';
            default: return 'Unknown';
        }
    }
}
