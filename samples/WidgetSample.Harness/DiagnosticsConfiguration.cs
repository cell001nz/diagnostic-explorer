using System;
using System.Drawing;
using System.Windows.Forms;
using DiagnosticExplorer;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Configuration;
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

        config.DefaultFormat<DateTime>("The date is {0:d MMM yyyy HH:mm:ss.fff}");
        config.DefaultFormat<Point>("Located at {0}");

        ConfigureForm(config);
        ConfigureWidget(config);
        ConfigureGadget(config);
    }

    private static void ConfigureEventRouting(EventSinkRouteOptions routes)
    {
        routes
            .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
            .Route("Widgets", route => route.AtLeast(LogLevel.Information).To("Widgets", "Widgets Events"))
            .Route("Gadgets", route => route.AtLeast(LogLevel.Information).To("Gadgets", "Gadget Events"))
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

            using (options.CreateCategoryScope("Widgets"))
            {
                options.Property(form => form.WidgetIdCount);
                options.Rate(form => form.WidgetEvents).ShowRate(false).ShowTotal();
            }

            using (options.CreateCategoryScope("Gadgets"))
            {
                options.Property(form => form.GadgetIdCount).Description("Max Gadget Id");
                options
                    .Rate(form => form.GadgetEvents)
                    .Category(f => $"Gadgets in Form {f.Name}")
                    .Description("The rate of gadget events received")
                    .ShowRate()
                    .ShowTotal();
            }

            using (options.CreateCategoryScope("All Gadgets"))
            {
                options
                    .Collection(form => form.Gadgets)
                    .List(opt => opt.Name(g => $"{g.Id} - {g.Name}").Category(g => g.Purpose).Description(g => $"Description for {g.Name}"))
                    .WithMaxItems(10);
            }

            options
                .CustomProperty("Computed", f => $"This form has {f.Controls.Count} controls")
                .Description(f => $"Control Info for {f.GetHashCode()}")
                .Category("ASDF");

            options.Collection(form => form.Widgets).Categories(widget => widget.FullName).WithMaxItems(10);
        });
    }

    private static void ConfigureWidget(IDiagConfigurator config)
    {
        config.Configure<Widget>(options =>
        {
            options.OptOut();
            options.Exclude(widget => widget.IgnoredProperty);
            options.Property(widget => widget.Name).AllowSet();
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
        });
    }
}
