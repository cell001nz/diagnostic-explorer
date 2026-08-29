using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WidgetSample.Harness;

internal static class DiagnosticsConfiguration
{
    private static WidgetConfig _widgetConfig = new();

    public static void Configure(IDiagConfigurator config, IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        config.ApplyAttributes = false;

        config.ConfigureHosting(configuration);
        config.ConfigureEventRouting(ConfigureEventRouting);
        config.RegisterObjects(RegisterObjects);

        config.DefaultFormat<DateTime>("{0:d MMM yyyy HH:mm:ss.fff}");
        config.DefaultFormat<Point>("Located at {0}");

        Form1.ConfigureDiagnostics(config);
        Widget.ConfigureDiagnostics(config);
        Gadget.ConfigureGadget(config);
        ConfigureWidgetConfig(config);
    }

    private static void RegisterObjects(IDiagRegistrar registrar)
    {
        var form1 = registrar.GetRequiredService<Form1>();

        registrar.RegisterService<Form1>("Form 1", "Main Form");
        registrar.Register(_widgetConfig, "My Config", "Main Form");
        foreach (var widget in form1.Widgets)
            registrar.Register(widget, widget.FullName, widget.FullName);
    }

    private static void ConfigureEventRouting(EventSinkRouteOptions routes)
    {
        routes
            .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
            .Route(typeof(Widget).FullName, route => route.AtLeast(LogLevel.Information).To(RouteValue.LoggerSuffix, "Widget Events2"))
            .Route(typeof(Gadget).FullName, route => route.AtLeast(LogLevel.Information).To("Form 1", "Gadget Events"))
            .Route(typeof(Form1).FullName, route => route.AtLeast(LogLevel.Trace).To("Form 1", "Form1 Events Only"))
            .Route("*", route => route.To("System", "Events"));
    }

    private static void ConfigureWidgetConfig(IDiagConfigurator config)
    {
        config.Configure<WidgetConfig>(options =>
        {
            options.IncludeAll();
            options.Exclude(obj => obj.Connection);
            options
                .Property(configuration => configuration.Items)
                .ListItems()
                .WithListItemName(item => $"Item: {item.Name}")
                .WithListItemCategory(item => "Items")
                .WithListItemValue(item => $"Capacity {item.Capacity}, tolerance {item.Tolerance:N2}")
                .WithListItemDescription(item => $"Installed {item.InstalledDate:d MMM yyyy}")
                .WithDrillDown()
                .WithExpandedHover();
        });
        config.Configure<WidgetConfig>(options =>
        {
            options.Property(instance => instance.Connection).WithCategory("Connection").Expand();
        });

        // config.Configure<WidgetConfigItem>(options => options.IncludeAll());
        // config.Configure<WidgetConnectionConfig>(options => options.IncludeAll());
    }
}
