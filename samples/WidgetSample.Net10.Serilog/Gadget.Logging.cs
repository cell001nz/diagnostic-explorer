namespace WidgetSample.Harness;

public partial class Gadget
{
    public global::Serilog.ILogger Log { get; private set; }

    internal Gadget(int id, global::Serilog.ILogger logger)
    {
        Id = id;
        Randomise();
        Log = logger.ForContext("SourceContext", $"{typeof(Gadget).FullName}.{FullName}");
    }

    internal void LogAdded() => Log.Information("Added gadget {GadgetId}", Id);
}
