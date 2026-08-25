using Microsoft.Extensions.Logging;

namespace WidgetSample.Harness;

public partial class Gadget
{
    private readonly ILogger _log;

    internal Gadget(int id, ILoggerFactory loggerFactory)
    {
        Id = id;
        Randomise();
        _log = loggerFactory.CreateLogger($"{typeof(Gadget).FullName}.{FullName}");
    }

    internal void LogAdded() => _log.LogInformation("Added gadget {GadgetId}", Id);
}
