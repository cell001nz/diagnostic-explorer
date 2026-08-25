namespace WidgetSample.Harness;

public partial class Gadget
{
    private readonly global::Serilog.ILogger _log;

    internal Gadget(int id, global::Serilog.ILogger logger)
    {
        Id = id;
        Randomise();
        _log = logger.ForContext("SourceContext", $"{typeof(Gadget).FullName}.{FullName}");
    }

    internal void LogAdded() => _log.Information("Added gadget {GadgetId}", Id);
}
