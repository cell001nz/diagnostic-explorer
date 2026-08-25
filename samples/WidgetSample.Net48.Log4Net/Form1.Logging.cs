using log4net;

namespace WidgetSample.Harness;

public partial class Form1
{
    private static ILog _gadgetLog;
    private static ILog _widgetLog;
    private static ILog _formLog;

    internal static void InitializeLoggers()
    {
        _gadgetLog = global::log4net.LogManager.GetLogger("Gadgets");
        _widgetLog = global::log4net.LogManager.GetLogger("Widgets");
        _formLog = global::log4net.LogManager.GetLogger(typeof(Form1));
    }

    private partial Gadget CreateGadget(int id) => new Gadget(id);

    private partial Widget CreateWidget(int id) => new Widget(id);
}
