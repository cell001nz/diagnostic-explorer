using log4net;

namespace WidgetSample.Harness;

public partial class Gadget
{
    public ILog Log { get; private set; }

    internal Gadget(int id)
    {
        Id = id;
        Randomise();
        Log = LogManager.GetLogger($"{typeof(Gadget).FullName}.{FullName}");
    }

    internal void LogAdded() => Log.Info($"Added gadget {Id}");
}
