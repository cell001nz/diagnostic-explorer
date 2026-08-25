using log4net;

namespace WidgetSample.Harness;

public partial class Gadget
{
    private readonly ILog _log;

    internal Gadget(int id)
    {
        Id = id;
        Randomise();
        _log = LogManager.GetLogger($"{typeof(Gadget).FullName}.{FullName}");
    }

    internal void LogAdded() => _log.Info($"Added gadget {Id}");
}
