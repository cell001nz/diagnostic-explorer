global using SampleLogger = Serilog.ILogger;

namespace WidgetSample.Harness;

public partial class Widget
{
    internal Widget(int id, global::Serilog.ILogger logger)
    {
        _id = id;
        Randomise();
        Log = logger.ForContext("SourceContext", $"{typeof(Widget).FullName}.{FullName}");
    }

    internal void LogAdded() => Log.Information("Added widget {WidgetId}", Id);
}
