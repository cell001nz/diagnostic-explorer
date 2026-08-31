using Microsoft.Extensions.Logging;

namespace WidgetSample.Harness;

public partial class Gadget
{
    public SampleLogger Log { get; private set; }

    internal Gadget(int id, ILoggerFactory loggerFactory)
    {
        Id = id;
        Randomise();
        Log = loggerFactory.CreateLogger($"{typeof(Gadget).FullName}.{FullName}");
    }

    internal void LogAdded() => Log.LogInformation("Added gadget {GadgetId}", Id);
}
