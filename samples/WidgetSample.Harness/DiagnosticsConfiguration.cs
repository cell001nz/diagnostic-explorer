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

        config.ApplyAttributes = false;
        config.ConfigureHosting(configuration);
        config.ConfigureEventRouting(ConfigureEventRouting);
        config.RegisterObjects(FindRegisteredObjects);

        config.DefaultFormat<DateTime>("The date is {0:d MMM yyyy HH:mm:ss.fff}");
        config.DefaultFormat<Point>("Located at {0}");

        ConfigureForm(config);
        ConfigureWidget(config);
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
            .Route(typeof(Widget).FullName, route => route.AtLeast(LogLevel.Information).To(RouteValue.LoggerSuffix, "Widget Events"))
            .Route(typeof(Gadget).FullName, route => route.AtLeast(LogLevel.Information).To("Form 1", "Gadget Events"))
            .Route("WidgetSample.Form1", route => route.AtLeast(LogLevel.Trace).To("Form 1", "Form1 Events Only"))
            .Route("*", route => route.AtLeast(LogLevel.Warning).AtMost(LogLevel.Warning).To("System", "Warnings"))
            .Route("*", route => route.AtLeast(LogLevel.Error).To("System", "Errors"));
    }

    private static void ConfigureForm(IDiagConfigurator config)
    {
        config.Configure<Form1>(options =>
        {
            options.OptIn();
            options.Extended(form => form.NullWidget).Category("NullWidget category");
            options.Property(form => form.InfoText).Named("Blah INFOTEXT").AllowSet();
            options.Property(form => form.SetMePlease).AllowSet();
            options.Property(form => form.Counter2);

            options
                .CustomProperty("WidgetCount", form => form.Widgets.Count)
                .Warn(form => form.Widgets.Count > 2, "Not too many widgets", "Widget count")
                .Error(form => form.Widgets.Count > 4, "Too many widgets", "Widget count");

            options
                .CustomProperty("Computed", f => $"This form has {f.Controls.Count} controls")
                .Description(f => $"Control Info for {f.GetHashCode()}");

            using (options.CreateCategoryScope("Widgets"))
            {
                options.Property(form => form.WidgetIdCount);
                options.Rate(form => form.WidgetEvents).ShowRate(false).ShowTotal();
                options.Collection(form => form.Widgets);
            }

            using (options.CreateCategoryScope("Gadgets"))
            {
                options.Property(form => form.GadgetIdCount).Description("Max Gadget Id");
                options.Rate(form => form.GadgetEvents).Description("The rate of gadget events received").ShowRate().ShowTotal();
            }

            using (options.CreateCategoryScope("All Gadgets"))
            {
                options
                    .Collection(form => form.Gadgets)
                    .List(opt => opt.Name(g => $"{g.Id} - {g.Name}").Category(g => g.Purpose).Description(g => $"Description for {g.Name}"))
                    .WithMaxItems(int.MaxValue);
            }
        });
    }

    private static void ConfigureWidgetConfig(IDiagConfigurator config)
    {
        config.Configure<WidgetConfig>(options =>
        {
            options.OptOut();
            options.Extended(configuration => configuration.Connection).Category("Connection");
            options
                .Collection(configuration => configuration.Items)
                .List(items =>
                    items
                        .Name(item => item.Name)
                        .Category(item => item.Purpose)
                        .Value(item => $"Capacity {item.Capacity}, tolerance {item.Tolerance:N2}")
                        .Description(item => $"Installed {item.InstalledDate:d MMM yyyy}")
                );
        });

        config.Configure<WidgetConfigItem>(options => options.OptOut());
        config.Configure<WidgetConnectionConfig>(options => options.OptOut());
    }

    private static void ConfigureWidget(IDiagConfigurator config)
    {
        config.Configure<Widget>(options =>
        {
            options.OptOut();
            options.Exclude(widget => widget.IgnoredProperty);
            options.Property(widget => widget.Name).AllowSet();
            options.Property(widget => widget.Configuration);
            using (options.CreateCategoryScope("Info"))
            {
                options.Property(widget => widget.DateCreated).AllowSet();
                options.Property(widget => widget.Size).AllowSet();
            }
        });
    }

    private static void ConfigureGadget(IDiagConfigurator config)
    {
        config.Configure<Gadget>(options =>
        {
            options.OptOut();
            options.Property(gadget => gadget.Name).AllowSet();
            options.Property(gadget => gadget.Purpose).AllowSet();
            options.Extended(gadget => gadget.Configuration).Category("Configuration");
        });
    }

    private static void ConfigureGadgetConfig(IDiagConfigurator config)
    {
        config.Configure<GadgetConfig>(options =>
        {
            options.OptOut();
            options.Extended(configuration => configuration.Power).Category("Power");
            options.Extended(configuration => configuration.Network).Category("Network");
            options.Extended(configuration => configuration.Maintenance).Category("Maintenance");
        });

        config.Configure<GadgetPowerConfig>(options => options.OptOut());
        config.Configure<GadgetNetworkConfig>(options => options.OptOut());
        config.Configure<GadgetMaintenanceConfig>(options => options.OptOut());
    }
}
