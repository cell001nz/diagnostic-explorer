using System;
using System.Drawing;
using DiagnosticExplorer;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WidgetSampleNuget;

internal static class DiagnosticsConfiguration
{
    public static void Configure(IDiagConfigurator config, IConfiguration configuration)
    {
        DiagExplorerOptions options = configuration.GetSection(DiagExplorerOptions.ConfigurationSectionName).Get<DiagExplorerOptions>() ?? new();

        config.ApplyAttributes = false;
        config.Runtime(runtime =>
        {
            runtime.Enabled(options.Enabled);
            runtime.RemoteUrl(options.RemoteUrl);
            runtime.SelfHostUrl(options.SelfHostUrl);
            runtime.EventRetention(retention =>
            {
                retention.MaxEventsPerSink = options.EventRetention.MaxEventsPerSink;
                retention.MaxAgeMinutes = options.EventRetention.MaxAgeMinutes;
            });
            runtime.Routing(routes => routes
                .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
                .Route("Widgets", route => route.AtLeast(LogLevel.Information).To("Widgets", "Widgets Events"))
                .Route("Gadgets", route => route.AtLeast(LogLevel.Information).To("Gadgets", "Gadget Events"))
                .Route("WidgetSampleNuget.Form1", route => route.AtLeast(LogLevel.Trace).To("Form 1", "Form1 Events Only"))
                .Route("*", route => route.AtLeast(LogLevel.Warning).AtMost(LogLevel.Warning).To("System", "Warnings"))
                .Route("*", route => route.AtLeast(LogLevel.Error).To("System", "Errors")));
        });

        config.DefaultFormat<DateTime>("The date is {0:d MMM yyyy HH:mm:ss.fff}");
        config.DefaultFormat<Point>("Located at {0}");

        config.Configure<Form1>(type =>
        {
            type.OptIn();
            type.Extended(form => form.NullWidget).Category("NullWidget category");
            type.Property(form => form.InfoText).Named("Blah INFOTEXT").AllowSet();
            type.Property(form => form.SetMePlease).AllowSet();
            type.Property(form => form.Counter2);
            type.CustomProperty("WidgetCount", form => form.Widgets.Count)
                .Warn(form => form.Widgets.Count > 2, "Not too many widgets", "Widget count")
                .Error(form => form.Widgets.Count > 4, "Too many widgets", "Widget count");

            using (type.CreateCategoryScope("Widgets"))
            {
                type.Property(form => form.WidgetIdCount);
                type.Rate(form => form.WidgetEvents).ShowRate(false).ShowTotal();
            }

            using (type.CreateCategoryScope("Gadgets"))
            {
                type.Property(form => form.GadgetIdCount).Description("Max Gadget Id");
                type.Rate(form => form.GadgetEvents)
                    .Category(form => $"Gadgets in Form {form.Name}")
                    .Description("The rate of gadget events received")
                    .ShowRate()
                    .ShowTotal();
            }

            using (type.CreateCategoryScope("All Gadgets"))
            {
                type.Collection(form => form.Gadgets)
                    .List(options => options.Name(gadget => $"{gadget.Id} - {gadget.Name}").Category(gadget => gadget.Purpose).Description(gadget => $"Description for {gadget.Name}"))
                    .WithMaxItems(10);
            }

            type.Collection(form => form.Widgets).Categories(widget => widget.FullName).WithMaxItems(10);
        });

        config.Configure<Widget>(type =>
        {
            type.OptOut();
            type.Exclude(widget => widget.IgnoredProperty);
            type.Property(widget => widget.Name).AllowSet();
            using (type.CreateCategoryScope("Info"))
            {
                type.Property(widget => widget.DateCreated).AllowSet();
                type.Property(widget => widget.Size).AllowSet();
            }
        });

        config.Configure<Gadget>(type =>
        {
            type.OptOut();
            type.Property(gadget => gadget.Name).AllowSet();
            type.Property(gadget => gadget.Purpose).AllowSet();
        });
    }
}
