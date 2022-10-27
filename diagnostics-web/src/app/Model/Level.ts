export class Level
{
  public static VERBOSE = 10_000;
  public static TRACE = 20_000;
  public static DEBUG = 30_000;
  public static INFO = 40_000;
  public static NOTICE = 50_000;
  public static WARN = 60_000;
  public static ERROR = 70_000;
  public static SEVERE = 80_000;
  public static CRITICAL = 90_000;
  public static ALERT = 100_000;
  public static FATAL = 110_000;
  public static EMERGENCY = 120_000;

  public static LevelToString(value: number): string
  {
    if (value >= this.EMERGENCY) return 'Emergency';
    if (value >= this.FATAL) return 'Fatal';
    if (value >= this.ALERT) return 'Alert';
    if (value >= this.CRITICAL) return 'Critical';
    if (value >= this.SEVERE) return 'Severe';
    if (value >= this.ERROR) return 'Error';
    if (value >= this.WARN) return 'Warn';
    if (value >= this.NOTICE) return 'Notice';
    if (value >= this.INFO) return 'Info';
    if (value >= this.DEBUG) return 'Debug';
    if (value >= this.TRACE) return 'Trace';
    if (value >= this.VERBOSE) return 'Verbose';

    return 'Unknown';
  }
}
