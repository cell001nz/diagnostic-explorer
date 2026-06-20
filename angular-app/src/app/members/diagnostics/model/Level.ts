export class Level {
    public static Verbose = 0;
    public static Debug = 1;
    public static Info = 2;
    public static Notice = 3;
    public static Warn = 4;
    public static Error = 5;
    public static Critical = 6;
    public static Fatal = 7;

    public static LevelToString(value: number): string {
        if (value == this.Verbose) return 'Verbose';
        if (value == this.Debug) return 'Debug';
        if (value == this.Info) return 'Info';
        if (value == this.Notice) return 'Notice';
        if (value == this.Warn) return 'Warn';
        if (value == this.Error) return 'Error';
        if (value == this.Critical) return 'Critical';
        if (value == this.Fatal) return 'Fatal';

        return 'Unknown';
    }
}
