global using SampleLogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.Extensions.Logging;

namespace WidgetSample.Harness;

public partial class Widget
{
    internal Widget(int id, ILoggerFactory loggerFactory)
    {
        _id = id;
        Randomise();
        _log = loggerFactory.CreateLogger($"{typeof(Widget).FullName}.{FullName}");
    }

    internal void LogAdded() => _log.LogInformation("Added widget {WidgetId}", Id);
}
