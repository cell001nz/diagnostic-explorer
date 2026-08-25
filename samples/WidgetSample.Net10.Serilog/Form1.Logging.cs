namespace WidgetSample.Harness;

public partial class Form1
{
    private static global::Serilog.ILogger _gadgetLog;
    private static global::Serilog.ILogger _widgetLog;
    private static global::Serilog.ILogger _formLog;
    private static global::Serilog.ILogger _logger;

    internal static void InitializeLoggers(global::Serilog.ILogger logger)
    {
        _logger = logger;
        _gadgetLog = logger.ForContext("SourceContext", "Gadgets");
        _widgetLog = logger.ForContext("SourceContext", "Widgets");
        _formLog = logger.ForContext("SourceContext", typeof(Form1).FullName);
    }

    private partial Gadget CreateGadget(int id) => new Gadget(id, _logger);

    private partial Widget CreateWidget(int id) => new Widget(id, _logger);
}
