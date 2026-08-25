global using SampleLogger = NLog.Logger;
using NLog;

namespace WidgetSample.Harness;

public partial class Widget
{
    internal Widget(int id)
    {
        _id = id;
        Randomise();
        _log = LogManager.GetLogger($"{typeof(Widget).FullName}.{FullName}");
    }

    internal void LogAdded() => _log.Info("Added widget {WidgetId}", Id);
}
