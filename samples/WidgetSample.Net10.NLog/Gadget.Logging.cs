using NLog;

namespace WidgetSample.Harness;

public partial class Gadget
{
    public Logger Log { get; private set; }

    internal Gadget(int id)
    {
        Id = id;
        Randomise();
        Log = LogManager.GetLogger($"{typeof(Gadget).FullName}.{FullName}");
    }

    internal void LogAdded() => Log.Info("Added gadget {GadgetId}", Id);
}
