using System;
using System.Collections.Generic;
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

        config.ConfigureHosting(configuration);
        config.ConfigureEventRouting(ConfigureEventRouting);
        config.ApplyAttributes = false;
        config.RegisterObjects(FindRegisteredObjects);

        config.DefaultFormat<DateTime>("The date is {0:d MMM yyyy HH:mm:ss.fff}");
        config.DefaultFormat<Point>("Located at {0}");

        Form1.ConfigureDiagnostics(config);
        Widget.ConfigureDiagnostics(config);
        ConfigureWidgetConfig(config);
        ConfigureGadget(config);
        ConfigureGadgetConfig(config);
    }

    private static IEnumerable<RegisteredObject> FindRegisteredObjects(IServiceProvider serviceProvider)
    {
        var form1 = serviceProvider.GetRequiredService<Form1>();

        yield return new RegisteredObject(form1, "Form 1", "Main Form");
        foreach (var widget in form1.Widgets)
            yield return new RegisteredObject(widget, widget.FullName, widget.FullName);
    }

    private static void ConfigureEventRouting(EventSinkRouteOptions routes)
    {
        routes
            .UseMatchMode(EventSinkRouteMatchMode.FirstMatch)
            .Route(typeof(Widget).FullName, route => route.AtLeast(LogLevel.Information).To(RouteValue.LoggerSuffix, "Widget Events2"))
            .Route(typeof(Gadget).FullName, route => route.AtLeast(LogLevel.Information).To("Form 1", "Gadget Events"))
            .Route("WidgetSample.Form1", route => route.AtLeast(LogLevel.Trace).To("Form 1", "Form1 Events Only"))
            .Route("*", route => route.AtLeast(LogLevel.Warning).AtMost(LogLevel.Warning).To("System", "Warnings"))
            .Route("*", route => route.AtLeast(LogLevel.Error).To("System", "Errors"));
    }

    private static void ConfigureWidgetConfig(IDiagConfigurator config)
    {
        config.Configure<WidgetConfig>(options =>
        {
            options.IncludeAll();
            options.Extended(configuration => configuration.Connection).Category("Connection");
            options
                .Collection(configuration => configuration.Items)
                .AsList(items =>
                    items
                        .Name(item => $"Item: {item.Name}")
                        .Category(item => "Items")
                        .Value(item => $"Capacity {item.Capacity}, tolerance {item.Tolerance:N2}")
                        .Description(item => $"Installed {item.InstalledDate:d MMM yyyy}")
                )
                .WithDrillDown();
        });

        config.Configure<WidgetConfigItem>(options => options.IncludeAll());
        config.Configure<WidgetConnectionConfig>(options => options.IncludeAll());
    }

    private static void ConfigureGadget(IDiagConfigurator config)
    {
        config.Configure<Gadget>(options =>
        {
            options.IncludeAll();
            options.Property(gadget => gadget.Name).AllowSet();
            options.Property(gadget => gadget.Purpose).AllowSet();
            options.Extended(gadget => gadget.Configuration).Category("Configuration");
        });

        config.ConfigureDrillDown<Gadget>(options =>
        {
            options.IncludeAll();
            options.Property(gadget => gadget.Name).AllowSet();
            options.Property(gadget => gadget.Configuration).Named("Gadget Config").AsDrillDownIcon();
            options.Extended(gadget => gadget.Configuration);
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
            options.Extended(configuration => configuration.Power).Category("Power");
            options.Extended(configuration => configuration.Network).Category("Network");
            options.Extended(configuration => configuration.Maintenance).Category("Maintenance");
        });

        config.Configure<GadgetPowerConfig>(options => options.IncludeAll());
        config.Configure<GadgetNetworkConfig>(options => options.IncludeAll());
        config.Configure<GadgetMaintenanceConfig>(options => options.IncludeAll());
    }
}
