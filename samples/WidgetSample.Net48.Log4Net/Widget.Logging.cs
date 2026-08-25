global using SampleLogger = log4net.ILog;
using log4net;

namespace WidgetSample.Harness;

public partial class Widget
{
    internal Widget(int id)
    {
        _id = id;
        Randomise();
        _log = LogManager.GetLogger($"{typeof(Widget).FullName}.{FullName}");
    }

    internal void LogAdded() => _log.Info($"Added widget {Id}");
}
