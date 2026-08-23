using System;

namespace DiagnosticExplorer;

public sealed class EventRetentionOptions
{
    public const string ConfigurationSectionKey = "DiagnosticExplorer:EventRetention";
    public const int DefaultMaxEventsPerSink = 1000;
    public const double DefaultMaxAgeMinutes = 30;

    public int MaxEventsPerSink { get; set; } = DefaultMaxEventsPerSink;

    public double MaxAgeMinutes { get; set; } = DefaultMaxAgeMinutes;

    public EventRetentionOptions WithMaxEventsPerSink(int maxEventsPerSink)
    {
        MaxEventsPerSink = maxEventsPerSink;
        return this;
    }

    public EventRetentionOptions WithMaxAge(TimeSpan maxAge)
    {
        MaxAgeMinutes = maxAge.TotalMinutes;
        return this;
    }

    internal EventRetentionOptions CloneAndValidate()
    {
        if (MaxEventsPerSink < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxEventsPerSink), "The maximum number of events per sink must be at least 1.");
        if (MaxAgeMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxAgeMinutes), "The maximum event age must be greater than zero.");

        return new EventRetentionOptions { MaxEventsPerSink = MaxEventsPerSink, MaxAgeMinutes = MaxAgeMinutes };
    }
}
