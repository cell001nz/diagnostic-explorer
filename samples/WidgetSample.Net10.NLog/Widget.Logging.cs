global using SampleLogger = NLog.Logger;
using NLog;

namespace WidgetSample.Harness;

public partial class Widget
{
    internal Widget(int id)
    {
        _id = id;
        Randomise();
        Log = LogManager.GetLogger($"{typeof(Widget).FullName}.{FullName}");
    }

    internal void LogAdded() => Log.Info("Added widget {WidgetId}", Id);
}
