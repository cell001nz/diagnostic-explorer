using System;
using Microsoft.Extensions.Logging;

namespace WidgetSampleNuget;

internal static class SampleLogging
{
    private static ILoggerFactory _factory;

    public static void Configure(ILoggerFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public static ILogger GetLogger(string category)
    {
        if (_factory == null)
            throw new InvalidOperationException("SampleLogging.Configure must be called before creating loggers.");

        return _factory.CreateLogger(category);
    }

    public static void Shutdown()
    {
        _factory = null;
    }
}
