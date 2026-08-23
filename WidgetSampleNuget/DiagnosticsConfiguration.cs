using DiagnosticExplorer;

namespace WidgetSampleNuget;

#if false // Enable after the DiagnosticExplorer package includes the fluent configuration API.
internal static class DiagnosticsConfiguration
{
    public static void ConfigureDiagnostics(IDiagConfigurator config)
    {
        config.ApplyAttributes = false;

        config.Configure<Form1>(type =>
        {
            type.OptIn();
            type.Extended(form => form.NullWidget);
            type.Property(form => form.GadgetIdCount).Category("Gadgets").Description("Max Gadeget Id");
            type.Property(form => form.WidgetIdCount).Category("Widgets");
            type.Property(form => form.InfoText).AllowSet();
            type.Property(form => form.SetMePlease).AllowSet();
            type.Property(form => form.Counter2).AllowSet(false);
            type.Rate(form => form.WidgetEvents).Category("Widgets").ShowRate(false).ShowTotal();
            type.Rate(form => form.GadgetEvents)
                .Category("Gadgets")
                .Description("The rate of gadget events received")
                .ShowRate()
                .ShowTotal();
            type.Collection(form => form.Gadgets).Category("All Gadgets").List().WithMaxItems(10);
            type.Collection(form => form.Widgets).Categories(widget => widget.FullName).WithMaxItems(10);
        });

        config.Configure<Widget>(type =>
        {
            type.OptOut();
            type.Exclude(widget => widget.IgnoredProperty);
            type.Property(widget => widget.Name).AllowSet();
            type.Property(widget => widget.DateCreated)
                .Category("Info")
                .Format("{0:d MMM yyyy HH:mm:ss}")
                .AllowSet();
            type.Property(widget => widget.Size).Category("Info").Format("Located at {0}").AllowSet();
        });

        config.Configure<Gadget>(type =>
        {
            type.OptOut();
            type.Property(gadget => gadget.Name).AllowSet();
            type.Property(gadget => gadget.Purpose).AllowSet();
        });

    }
}
#endif
