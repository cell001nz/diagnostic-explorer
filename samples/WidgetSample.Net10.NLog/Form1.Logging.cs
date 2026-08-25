using NLog;

namespace WidgetSample.Harness;

public partial class Form1
{
    private static Logger _gadgetLog;
    private static Logger _widgetLog;
    private static Logger _formLog;

    internal static void InitializeLoggers()
    {
        _gadgetLog = LogManager.GetLogger("Gadgets");
        _widgetLog = LogManager.GetLogger("Widgets");
        _formLog = LogManager.GetLogger(typeof(Form1).FullName);
    }

    private partial Gadget CreateGadget(int id) => new Gadget(id);

    private partial Widget CreateWidget(int id) => new Widget(id);
}
