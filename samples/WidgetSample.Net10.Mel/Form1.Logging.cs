using Microsoft.Extensions.Logging;

namespace WidgetSample.Harness;

public partial class Form1
{
    private readonly ILogger _gadgetLog;
    private readonly ILogger _widgetLog;
    private readonly ILogger _formLog;
    private readonly ILoggerFactory _loggerFactory;

    public Form1(ILoggerFactory loggerFactory, ILogger<Form1> logger)
        : this()
    {
        _loggerFactory = loggerFactory;
        _gadgetLog = loggerFactory.CreateLogger("Gadgets");
        _widgetLog = loggerFactory.CreateLogger("Widgets");
        _formLog = loggerFactory.CreateLogger(typeof(Form1).FullName);
    }

    private partial Gadget CreateGadget(int id) => new Gadget(id, _loggerFactory);

    private partial Widget CreateWidget(int id) => new Widget(id, _loggerFactory);
}
