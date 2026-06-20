export class Level {
    // Microsoft.Extensions.Logging.LogLevel ordinals
    public static Trace = 0;
    public static Debug = 1;
    public static Information = 2;
    public static Warning = 3;
    public static Error = 4;
    public static Critical = 5;
    public static None = 6;

    public static LevelToString(value: number): string {
        switch (value) {
            case Level.Trace: return 'Trace';
            case Level.Debug: return 'Debug';
            case Level.Information: return 'Info';
            case Level.Warning: return 'Warn';
            case Level.Error: return 'Error';
            case Level.Critical: return 'Critical';
            case Level.None: return 'None';
            default: return 'Unknown';
        }
    }
}
