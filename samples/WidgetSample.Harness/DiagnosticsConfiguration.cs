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
        ConfigureWidgetConfig(config);
        ConfigureGadget(config);
        ConfigureGadgetConfig(config);
    }

    private static void RegisterObjects(IDiagRegistrar registrar)
    {
        var form1 = registrar.GetRequiredService<Form1>();

        registrar.RegisterService<Form1>("Form 1", "Main Form");
        foreach (var widget in form1.Widgets)
            registrar.Register(widget, widget.FullName, widget.FullName);
    }

    private static void ConfigureEventRouting(EventSinkRouteOptions routes)
    {
        routes
            .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
            .Route(typeof(Widget).FullName, route => route.AtLeast(LogLevel.Information).To(RouteValue.LoggerSuffix, "Widget Events2"))
            .Route(typeof(Gadget).FullName, route => route.AtLeast(LogLevel.Information).To("Form 1", "Gadget Events"))
            .Route("WidgetSample.Form1", route => route.AtLeast(LogLevel.Trace).To("Form 1", "Form1 Events Only"))
            .Route("*", route => route.To("System", "Events"));
    }

    private static void ConfigureWidgetConfig(IDiagConfigurator config)
    {
        config.Configure<WidgetConfig>(options =>
        {
            options.IncludeAll();
            options.Expanded(configuration => configuration.Connection).Category("Connection");
            options
                .Collection(configuration => configuration.Items)
                .AsList(items =>
                    items
                        .Name(item => $"Item: {item.Name}")
                        .Category(item => "Items")
                        .Value(item => $"Capacity {item.Capacity}, tolerance {item.Tolerance:N2}")
                        .Description(item => $"Installed {item.InstalledDate:d MMM yyyy}")
                )
                .AsDrillDown();
        });

        // config.Configure<WidgetConfigItem>(options => options.IncludeAll());
        // config.Configure<WidgetConnectionConfig>(options => options.IncludeAll());
    }

    private static void ConfigureGadget(IDiagConfigurator config)
    {
        config.Configure<Gadget>(options =>
        {
            options.IncludeAll();
            options.Property(gadget => gadget.Name).AllowSet();
            options.Property(gadget => gadget.Purpose).AllowSet();
            options.Expanded(gadget => gadget.Configuration).Category("Configuration");
        });

        config.ConfigureDrillDown<Gadget>(options =>
        {
            options.IncludeAll();
            options.Property(gadget => gadget.Name).AllowSet();
            options.Property(gadget => gadget.Configuration).Named("Gadget Config").AsDrillDownIcon();
            options.Expanded(gadget => gadget.Configuration);
            options.Route(
                gadget => $"{typeof(Gadget).FullName}.{gadget.FullName}",
                LoggerNameMatchMode.Exact,
                route => route.To("Gadget", "Gadget Events")
            );
        });
    }

    private static void ConfigureGadgetConfig(IDiagConfigurator config)
    {
        config.Configure<GadgetConfig>(options =>
        {
            options.IncludeAll();
            // options.Expanded(obj => obj.Power).Category("Power");
            options.Property(obj => obj.CommissionedOn).AsDateOnly();
            options.Property(obj => obj.Power).AsJson(100).WithJsonHover().WithDrillDown();
            options.Property(obj => obj.Power).AsJson(100).WithJsonHover().WithDrillDown();
            options
                .Property("Network2 This has a very very long name which is very long", obj => obj.Network)
                .AsJson(100)
                .WithExpandedHover()
                .WithDrillDown();
            options.Expanded(obj => obj.Network).Category("Network");
            options.Expanded(obj => obj.Maintenance).Category("Maintenance");
        });

        config.Configure<GadgetPowerConfig>(options =>
        {
            options.IncludeAll();
        });
        // config.Configure<GadgetNetworkConfig>(options => options.IncludeAll());
        // config.Configure<GadgetMaintenanceConfig>(options => options.IncludeAll());
    }
}
