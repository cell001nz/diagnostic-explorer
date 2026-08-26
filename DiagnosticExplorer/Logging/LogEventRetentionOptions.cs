using System;

namespace DiagnosticExplorer.Logging;

public sealed class LogEventRetentionOptions
{
    public const int DefaultMaxEvents = 5000;
    public const double DefaultMaxAgeMinutes = 5;

    public int MaxEvents { get; set; } = DefaultMaxEvents;

    public double MaxAgeMinutes { get; set; } = DefaultMaxAgeMinutes;

    public LogEventRetentionOptions WithMaxEvents(int maxEvents)
    {
        MaxEvents = maxEvents;
        return this;
    }

    public LogEventRetentionOptions WithMaxAge(TimeSpan maxAge)
    {
        MaxAgeMinutes = maxAge.TotalMinutes;
        return this;
    }

    internal LogEventRetentionOptions CloneAndValidate()
    {
        if (MaxEvents <= 0)
            throw new InvalidOperationException("The log stream maximum event count must be greater than zero.");
        if (MaxAgeMinutes <= 0)
            throw new InvalidOperationException("The log stream maximum age must be greater than zero.");

        return new LogEventRetentionOptions { MaxEvents = MaxEvents, MaxAgeMinutes = MaxAgeMinutes };
    }
}
