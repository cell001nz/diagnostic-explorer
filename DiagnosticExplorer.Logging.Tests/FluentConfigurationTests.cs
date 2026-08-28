using System;
using System.Collections.Generic;
using System.Linq;
using DiagnosticExplorer.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DiagnosticExplorer.Logging.Tests;

public sealed class FluentConfigurationTests : IDisposable
{
    public FluentConfigurationTests()
    {
        DiagnosticManager.UseConfiguration(new DiagnosticConfiguration());
    }

    public void Dispose()
    {
        DiagnosticManager.UseConfiguration(new DiagnosticConfiguration());
    }

    [Fact]
    public void EmptyConfigurationPreservesAttributeBehavior()
    {
        PropertyBag bag = Render(new AttributeSample());

        Property property = bag.GetProperty("Attributed name", "Source");
        Assert.NotNull(property);
        Assert.Equal("0007", property.Value);
        Assert.Equal("Source description", property.Description);
        Assert.True(property.CanSet);
        Assert.Null(bag.GetProperty(nameof(AttributeSample.Plain)));
        Assert.Null(bag.GetProperty(nameof(AttributeSample.Ignored)));
    }

    [Fact]
    public void AttributesCanBeDisabledGlobally()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new AttributeSample());

        Property property = bag.GetProperty(nameof(AttributeSample.Attributed), "General");
        Assert.NotNull(property);
        Assert.Equal("7", property.Value);
        Assert.Null(property.Description);
        Assert.False(property.CanSet);
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Ignored), "General"));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Hidden), "General"));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Plain), "General"));
        Assert.DoesNotContain(bag.Categories, category => category.Name == "Source");
        Assert.All(bag.Categories.Single(category => category.Name == "General").Properties, item => Assert.NotNull(item));
    }

    [Fact]
    public void EmptyConfigurationUsesUsefulPropertyDefaults()
    {
        PropertyBag bag = Render(new DefaultScalarSample());

        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Text), "General"));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Status), "General"));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Count), "General"));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.OptionalCount), "General"));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Amount), "General"));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Created), "General"));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Duration), "General"));
        Property child = bag.GetProperty(nameof(DefaultScalarSample.Child), "General");
        Assert.True(child.CanDrillDown);
        Assert.True(child.DrillDownIconOnly);
        Assert.Null(child.Value);
        Assert.Equal("2", bag.GetProperty(nameof(DefaultScalarSample.Items), "General").Value);
        Assert.Null(bag.GetProperty(nameof(DefaultScalarSample.Pending), "General"));
    }

    [Fact]
    public void RenderedPropertiesExposeValueKinds()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<ValueKindSample>(type => type.IncludeAll());
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ValueKindSample());

        Assert.Equal(PropertyValueKind.Text, bag.GetProperty(nameof(ValueKindSample.Text), "General").ValueKind);
        Assert.Equal(PropertyValueKind.PositiveNumber, bag.GetProperty(nameof(ValueKindSample.Positive), "General").ValueKind);
        Assert.Equal(PropertyValueKind.ZeroNumber, bag.GetProperty(nameof(ValueKindSample.Zero), "General").ValueKind);
        Assert.Equal(PropertyValueKind.NegativeNumber, bag.GetProperty(nameof(ValueKindSample.Negative), "General").ValueKind);
        Assert.Equal(PropertyValueKind.DateTime, bag.GetProperty(nameof(ValueKindSample.Timestamp), "General").ValueKind);
        Assert.Equal(PropertyValueKind.Duration, bag.GetProperty(nameof(ValueKindSample.Duration), "General").ValueKind);
        Assert.Equal(PropertyValueKind.Boolean, bag.GetProperty(nameof(ValueKindSample.Enabled), "General").ValueKind);
        Assert.Equal(PropertyValueKind.Enumeration, bag.GetProperty(nameof(ValueKindSample.Status), "General").ValueKind);
        Assert.Equal(PropertyValueKind.Object, bag.GetProperty(nameof(ValueKindSample.Details), "General").ValueKind);
    }

    [Fact]
    public void RuntimeSettingsCanBeConfiguredFluently()
    {
        DiagnosticConfiguration configuration = new();
        configuration.ConfigureHosting(runtime =>
            runtime
                .Enabled(false)
                .AddHost(DiagnosticHostType.Remote, "http://localhost:2803/diagnostics")
                .AddHost(DiagnosticHostType.SelfHost, "http://localhost:45000")
                .EventRetention(retention => retention.WithMaxEventsPerSink(1000).WithMaxAge(TimeSpan.FromMinutes(30)))
        );
        configuration.ConfigureEventRouting(routes =>
            routes
                .UseMatchMode(EventSinkRouteMatchMode.AllMatches)
                .Route("Widgets", route => route.AtLeast(LogLevel.Information).To("Widgets", "Widgets Events"))
                .Route("*", route => route.AtLeast(LogLevel.Warning).AtMost(LogLevel.Warning).To("System", "Warnings"))
        );

        DiagnosticManager.UseConfiguration(configuration);

        Assert.False(DiagnosticManager.Enabled);
        Assert.Collection(
            configuration.RuntimeOptions.Hosts,
            host => Assert.Equal((DiagnosticHostType.Remote, "http://localhost:2803/diagnostics"), (host.Type, host.Url)),
            host => Assert.Equal((DiagnosticHostType.SelfHost, "http://localhost:45000"), (host.Type, host.Url))
        );
        Assert.Equal(1000, EventSinkRepo.Default.EventRetention.MaxEventsPerSink);
        Assert.Equal(30, EventSinkRepo.Default.EventRetention.MaxAgeMinutes);
        Assert.Equal(EventSinkRouteMatchMode.AllMatches, configuration.RuntimeOptions.Routing.MatchMode);
        new ServiceCollection().AddDiagnosticExplorer(configuration);
        Assert.Collection(
            configuration.RuntimeOptions.Routing.Routes,
            route =>
            {
                Assert.Equal("Widgets", route.CategoryPattern);
                Assert.Equal(LogLevel.Information, route.MinLevel);
                Assert.Collection(
                    route.Destinations,
                    destination =>
                    {
                        Assert.Equal(RouteValueSource.Fixed, destination.SinkCategory.Source);
                        Assert.Equal("Widgets", destination.SinkCategory.Value);
                        Assert.Equal(RouteValueSource.Fixed, destination.SinkName.Source);
                        Assert.Equal("Widgets Events", destination.SinkName.Value);
                    }
                );
            },
            route =>
            {
                Assert.Equal("*", route.CategoryPattern);
                Assert.Equal(LogLevel.Warning, route.MinLevel);
                Assert.Equal(LogLevel.Warning, route.MaxLevel);
            }
        );
    }

    [Fact]
    public void RouteValuesBindFromConfiguration()
    {
        Dictionary<string, string> values = new()
        {
            ["Routes:0:CategoryPattern"] = "WidgetSample.Harness.Widget",
            ["Routes:0:Destinations:0:SinkCategory:Source"] = "LoggerSuffix",
            ["Routes:0:Destinations:0:SinkName"] = "Widget Events",
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        EventSinkRouteOptions options = configuration.Get<EventSinkRouteOptions>();
        EventSinkDestination destination = Assert.Single(Assert.Single(options.Routes).Destinations);

        Assert.Equal(RouteValueSource.LoggerSuffix, destination.SinkCategory.Source);
        Assert.Equal(RouteValueSource.Fixed, destination.SinkName.Source);
        Assert.Equal("Widget Events", destination.SinkName.Value);
    }

    [Fact]
    public void HostingConfigurationRegistersConfiguredLocalAndRemoteHosts()
    {
        DiagnosticConfiguration configuration = new();
        configuration.ConfigureHosting(hosting =>
            hosting
                .AddHost(DiagnosticHostType.Remote, "http://localhost:2803/diagnostics")
                .AddHost(DiagnosticHostType.SelfHost, "http://127.0.0.1:50001")
        );

        ServiceCollection services = new();
        services.AddDiagnosticExplorer(configuration);

        ServiceDescriptor[] hostedServices = services.Where(service => service.ServiceType == typeof(IHostedService)).ToArray();
        Assert.Equal(2, hostedServices.Length);
        Assert.Contains(hostedServices, service => service.ImplementationType?.Name == "DiagnosticSelfHostHostedService");
        Assert.Contains(hostedServices, service => service.ImplementationFactory != null);
    }

    [Fact]
    public void HostingConfigurationBindsTypedHostsFromConfiguration()
    {
        IConfiguration source = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    ["DiagnosticExplorer:Hosts:0:Type"] = "Remote",
                    ["DiagnosticExplorer:Hosts:0:Url"] = "http://localhost:2803/diagnostics",
                    ["DiagnosticExplorer:Hosts:1:Type"] = "SelfHost",
                    ["DiagnosticExplorer:Hosts:1:Url"] = "http://127.0.0.1:50001",
                }
            )
            .Build();
        DiagnosticConfiguration configuration = new();

        configuration.ConfigureHosting(source);

        Assert.Collection(
            configuration.RuntimeOptions.Hosts,
            host => Assert.Equal((DiagnosticHostType.Remote, "http://localhost:2803/diagnostics"), (host.Type, host.Url)),
            host => Assert.Equal((DiagnosticHostType.SelfHost, "http://127.0.0.1:50001"), (host.Type, host.Url))
        );
    }

    [Fact]
    public void ConfigureBuildsAppliesAndReturnsConfiguration()
    {
        DiagnosticConfiguration configuration = DiagnosticManager.Configure(config =>
            config.ConfigureHosting(runtime => runtime.Enabled(false).AddHost(DiagnosticHostType.SelfHost, "http://localhost:45000"))
        );

        Assert.Same(configuration, DiagnosticManager.CurrentConfiguration);
        Assert.False(DiagnosticManager.Enabled);
        Assert.Single(configuration.RuntimeOptions.Hosts);
        Assert.Equal("http://localhost:45000", configuration.RuntimeOptions.Hosts[0].Url);
    }

    [Fact]
    public void DefaultFormatAppliesToTypesAndNullableVariants()
    {
        DiagnosticConfiguration configuration = new();
        configuration.DefaultFormat<DateTime>("d MMM yyyy HH:mm:ss");
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new DefaultFormatSample());

        Assert.Equal("2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.Value), "General").Value);
        Assert.Equal("2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.NullableValue), "General").Value);

        configuration.DefaultFormat<DateTime>("The date is {0:d MMM yyyy HH:mm:ss}");
        DiagnosticManager.UseConfiguration(configuration);

        bag = Render(new DefaultFormatSample());

        Assert.Equal("The date is 2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.Value), "General").Value);
        Assert.Equal("The date is 2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.NullableValue), "General").Value);
    }

    [Fact]
    public void FluentPropertyCanUseTypedDelegateFormatter()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Attributed).Format(value => $"Value: {value:D3}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new AttributeSample()).GetProperty("Attributed name", "Source");

        Assert.Equal("Value: 007", property.Value);
    }

    [Fact]
    public void FluentPropertyCanRenderAsJson()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<JsonFormatSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Details).AsJson();
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new JsonFormatSample()).GetProperty(nameof(JsonFormatSample.Details), "General");

        Assert.Equal("{\"Value\":\"JSON\"}", property.Value);
    }

    [Fact]
    public void FluentPropertyCanRenderDateOnly()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DefaultFormatSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Value).AsDateOnly();
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new DefaultFormatSample()).GetProperty(nameof(DefaultFormatSample.Value), "General");

        Assert.Equal(new DateTime(2025, 1, 2).ToString("d"), property.Value);
        Assert.Equal(PropertyValueKind.DateTime, property.ValueKind);
    }

    [Fact]
    public void FluentPropertyCanLimitJsonOutput()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<JsonFormatSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Details).AsJson(12);
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new JsonFormatSample()).GetProperty(nameof(JsonFormatSample.Details), "General");

        Assert.Equal("{\"Value\":\"JS", property.Value);
    }

    [Fact]
    public void FluentPropertyMetadataCanUseContainingObjectDelegates()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Attributed)
                .Named(sample => $"Value {sample.Attributed}")
                .Category(sample => $"Source {sample.Attributed}")
                .Description(sample => $"Description {sample.Attributed}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new AttributeSample()).GetProperty("Value 7", "Source 7");

        Assert.Equal("Description 7", property.Description);
    }

    [Fact]
    public void FluentPropertyWarningUsesContainingObject()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<WarningSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Current)
                .Warn(sample => sample.Current < sample.Minimum, sample => $"Current value {sample.Current} is below {sample.Minimum}", "Current")
                .Error(sample => sample.Current < 0, sample => $"Current value {sample.Current} is invalid", "Current");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property warningProperty = Render(new WarningSample(3, 5)).GetProperty(nameof(WarningSample.Current), "General");
        Property errorProperty = Render(new WarningSample(-1, 5)).GetProperty(nameof(WarningSample.Current), "General");
        Property validProperty = Render(new WarningSample(5, 3)).GetProperty(nameof(WarningSample.Current), "General");

        Assert.Collection(
            warningProperty.Alerts,
            alert =>
            {
                Assert.Equal("Current value 3 is below 5", alert.Message);
                Assert.Equal(PropertyAlertSeverity.Warning, alert.Severity);
                Assert.Equal("Current", alert.Category);
            }
        );
        Assert.Collection(
            errorProperty.Alerts,
            alert =>
            {
                Assert.Equal("Current value -1 is invalid", alert.Message);
                Assert.Equal(PropertyAlertSeverity.Error, alert.Severity);
                Assert.Equal("Current", alert.Category);
            }
        );
        Assert.Empty(validProperty.Alerts);
    }

    [Fact]
    public void CategoryScopeAppliesToPropertyDeclarationsAndCanBeOverridden()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            using (type.CreateCategoryScope("Scoped"))
            {
                type.Property(sample => sample.First);
                type.Property("Computed", sample => sample.First + sample.Second);
                type.Property(sample => sample.Second).Category("Specific");
            }
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ReplacementSample());

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), "Scoped"));
        Assert.NotNull(bag.GetProperty("Computed", "Scoped"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), "Specific"));
    }

    [Fact]
    public void NamedPropertyUsesDelegateAndFluentMetadata()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property("Computed", sample => sample.First + sample.Second)
                .Category(sample => $"Calculated {sample.First}")
                .Description(sample => $"Combined {sample.First} and {sample.Second}")
                .Warn(sample => sample.First == "First", "First value needs attention")
                .Error(sample => sample.Second == null, "Second value is required");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new ReplacementSample()).GetProperty("Computed", "Calculated First");

        Assert.Equal("FirstSecond", property.Value);
        Assert.Equal("Combined First and Second", property.Description);
        Assert.False(property.CanSet);
        Assert.Collection(
            property.Alerts,
            alert =>
            {
                Assert.Equal(PropertyAlertSeverity.Warning, alert.Severity);
                Assert.Equal("First value needs attention", alert.Message);
                Assert.Equal(alert.Message, alert.Category);
            }
        );
    }

    [Fact]
    public void NamedPropertyCanRenderAsJson()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property("Computed", sample => sample.First + sample.Second).AsJson();
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new ReplacementSample()).GetProperty("Computed", "General");

        Assert.Equal("\"FirstSecond\"", property.Value);
    }

    [Fact]
    public void NamedPropertyCanRenderAsDrillDownIcon()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property("Details", sample => sample.Details).AsDrillDownIcon("View more details");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new StrategySample()).GetProperty("Details", "General");

        Assert.True(property.CanDrillDown);
        Assert.True(property.DrillDownIconOnly);
        Assert.Equal("View more details", property.DrillDownText);
        Assert.Null(property.Value);
    }

    [Fact]
    public void PropertyCanFetchJsonHoverOnDemand()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithJsonHover();
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        Property property = Render(root).GetProperty(nameof(DrillDownRoot.Child), "General");

        Assert.True(property.CanJsonHover);
        Assert.False(property.CanExpandedHover);
        Assert.False(property.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest
            {
                ObjectPaths = new List<string> { "Tests|Root|General|Child" },
                JsonHover = true,
                ExcludeEventViews = true,
            }
        );

        Assert.False(string.IsNullOrWhiteSpace(response.Json));
        Assert.Contains(Environment.NewLine, response.Json);
        Assert.Empty(response.EventViews);
    }

    [Fact]
    public void PropertyCanRenderExpandedHoverWithoutEventViews()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithExpandedHover();
        });
        configuration.ConfigureDrillDown<ChildSample>(type =>
            type.Route("Tests.Child", LoggerNameMatchMode.Exact, route => route.To("Hover", "Events"))
        );
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        Property property = Render(root).GetProperty(nameof(DrillDownRoot.Child), "General");

        Assert.False(property.CanJsonHover);
        Assert.True(property.CanExpandedHover);
        Assert.False(property.CanDrillDown);

        DrillDownResponse normalResponse = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root|General|Child" } }
        );
        Assert.NotEmpty(normalResponse.EventViews);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest
            {
                ObjectPaths = new List<string> { "Tests|Root|General|Child" },
                ExcludeEventViews = true,
            }
        );

        Assert.NotEmpty(response.Diagnostics.PropertyBags);
        Assert.Empty(response.EventViews);
    }

    [Fact]
    public void InlineCustomProjectionCanRenderSimpleExtendedCollectionAndRateProperties()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
            type.Custom(
                    "Summary",
                    projection =>
                    {
                        projection.Property("Name", root => "Projection");
                        projection.Property("Child", root => root.Child).Expand();
                        projection
                            .Property("Items", root => root.Items)
                            .ListItems(list => list.Name(item => item.Name).Value(item => item.Value.ToString()));
                        projection.Property("Requests", root => root.Requests).ShowRate(false).ShowTotal();
                    }
                )
                .AsDrillDownIcon()
        );
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        root.Requests.Register(5);
        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root|General|Summary" } }
        );

        PropertyBag bag = Assert.Single(response.Diagnostics.PropertyBags);
        Assert.Equal("Projection", bag.GetProperty("Name", "General").Value);
        Assert.Null(bag.GetProperty("Child", "General"));
        Assert.NotNull(bag.GetProperty(nameof(ChildSample.Name), "Child"));
        Assert.Equal("1", bag.GetProperty("One", "General").Value);
        Assert.Equal("5", bag.GetProperty("Total Requests", "General").Value);
    }

    [Fact]
    public void FluentMetadataOverridesOnlyExplicitAttributeValues()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Attributed).Named("Configured name").Category("Configured");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new AttributeSample()).GetProperty("Configured name", "Configured");

        Assert.NotNull(property);
        Assert.Equal("0007", property.Value);
        Assert.Equal("Source description", property.Description);
        Assert.True(property.CanSet);
    }

    [Fact]
    public void ExplicitInclusionAndExclusionOverrideAttributes()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.ExcludeAll();
            type.Exclude(sample => sample.Attributed);
            type.Include(sample => sample.Ignored);
            type.Include(sample => sample.Hidden);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new AttributeSample());

        Assert.Null(bag.GetProperty("Attributed name", "Source"));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Ignored), "General"));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Hidden), "General"));
        Assert.Null(bag.GetProperty(nameof(AttributeSample.Plain)));
    }

    [Fact]
    public void IncludeAllIncludesPublicPropertiesUnlessExplicitlyExcluded()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.IncludeAll();
            type.Exclude(sample => sample.Second);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ReplacementSample());

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), "General"));
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.Second), "General"));
    }

    [Fact]
    public void PropertiesWithoutCategoriesDefaultToGeneral()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.First);
            type.Include(sample => sample.Second);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ReplacementSample());

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), "General"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), "General"));
    }

    [Fact]
    public void CollectionCanExposeCountAndLimitedConcatenatedValues()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ShowCount().ConcatItems(", ").WithMaxItems(2);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("3", bag.GetProperty("Items count", "General").Value);
        Assert.Equal("3 items: One, Two, ... (1 more item)", bag.GetProperty("Items", "General").Value);
    }

    [Fact]
    public void PropertyCollectionsSupportCommonGenericCollectionInterfaces()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionInterfaceSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Array).Category("Array").ListItems(list => list.Name(item => item.Name));
            type.Property(sample => sample.List).Category("List").ListItems(list => list.Name(item => item.Name));
            type.Property(sample => sample.ReadOnlyList).Category("Read-only list").ListItems(list => list.Name(item => item.Name));
            type.Property(sample => sample.Collection).Category("Collection").ListItems(list => list.Name(item => item.Name));
            type.Property(sample => sample.ReadOnlyCollection).Category("Read-only collection").ListItems(list => list.Name(item => item.Name));
            type.Property(sample => sample.Set).Category("Set").ListItems(list => list.Name(item => item.Name));
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionInterfaceSample());

        Assert.NotNull(bag.GetProperty("Array", "Array"));
        Assert.NotNull(bag.GetProperty("List", "List"));
        Assert.NotNull(bag.GetProperty("Read-only list", "Read-only list"));
        Assert.NotNull(bag.GetProperty("Collection", "Collection"));
        Assert.NotNull(bag.GetProperty("Read-only collection", "Read-only collection"));
        Assert.NotNull(bag.GetProperty("Set", "Set"));
    }

    [Fact]
    public void CollectionPresentationMethodsSupportAllRecognizedCollectionShapes()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionInterfaceSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Array).ShowCount().ConcatItems(", ").SectionByItem(item => item.Name).WithMaxItems(1);
            type.Property(sample => sample.List).ShowCount().ConcatItems(", ").SectionByItem(item => item.Name).WithMaxItems(1);
            type.Property(sample => sample.ReadOnlyList).ShowCount().ConcatItems(", ").SectionByItem(item => item.Name).WithMaxItems(1);
            type.Property(sample => sample.Collection).ShowCount().ConcatItems(", ").SectionByItem(item => item.Name).WithMaxItems(1);
            type.Property(sample => sample.ReadOnlyCollection).ShowCount().ConcatItems(", ").SectionByItem(item => item.Name).WithMaxItems(1);
            type.Property(sample => sample.Set).ShowCount().ConcatItems(", ").SectionByItem(item => item.Name).WithMaxItems(1);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionInterfaceSample());

        Assert.Equal("1 item: Array", bag.GetProperty(nameof(CollectionInterfaceSample.Array), "General").Value);
        Assert.Equal("1 item: List", bag.GetProperty(nameof(CollectionInterfaceSample.List), "General").Value);
        Assert.Equal("1 item: Read-only list", bag.GetProperty(nameof(CollectionInterfaceSample.ReadOnlyList), "General").Value);
        Assert.Equal("1 item: Collection", bag.GetProperty(nameof(CollectionInterfaceSample.Collection), "General").Value);
        Assert.Equal("1 item: Read-only collection", bag.GetProperty(nameof(CollectionInterfaceSample.ReadOnlyCollection), "General").Value);
        Assert.Equal("1 item: Set", bag.GetProperty(nameof(CollectionInterfaceSample.Set), "General").Value);
    }

    [Fact]
    public void NamedDelegateCollectionAndExtendedUseExistingStrategyRenderers()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            CollectionSample.ConfigurePrivateItems(type);
        });
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            StrategySample.ConfigurePrivateDetails(type);
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag collectionBag = Render(new CollectionSample());
        PropertyBag extendedBag = Render(new StrategySample());

        Assert.Equal("1", collectionBag.GetProperty("One", "General").Value);
        Assert.Equal("2", collectionBag.GetProperty("Two", "General").Value);
        Assert.Equal("Nested", extendedBag.GetProperty(nameof(ChildSample.Name), "Private details").Value);
    }

    [Fact]
    public void DirectFieldCollectionAndExtendedUseExistingStrategyRenderers()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            CollectionSample.ConfigurePrivateItemsFromField(type);
        });
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            StrategySample.ConfigurePrivateDetailsFromField(type);
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag collectionBag = Render(new CollectionSample());
        PropertyBag extendedBag = Render(new StrategySample());

        Assert.Equal("1", collectionBag.GetProperty("One", "General").Value);
        Assert.Equal("2", collectionBag.GetProperty("Two", "General").Value);
        Assert.Equal("Nested", extendedBag.GetProperty(nameof(ChildSample.Name), "_privateDetails").Value);
    }

    [Fact]
    public void DirectFieldPropertyUsesDelegateConfiguration()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<PrivatePropertySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample._value).Description("Private value");
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new PrivatePropertySample());

        Assert.Equal("Value", bag.GetProperty("_value", "General").Value);
        Assert.Equal("Private value", bag.GetProperty("_value", "General").Description);
    }

    [Fact]
    public void DirectFieldRateUsesRateRenderer()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample._privateRequests).ShowRate(false).ShowTotal();
        });
        DiagnosticManager.UseConfiguration(configuration);

        StrategySample sample = new();
        sample._privateRequests.Register(4);
        PropertyBag bag = Render(sample);

        Assert.Equal("4", bag.GetProperty("Total _privateRequests", "General").Value);
        Assert.Null(bag.GetProperty("_privateRequests/sec", "General"));
    }

    [Fact]
    public void CollectionCategoriesUseTypedSelectorAndMaxItems()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).SectionByItem(item => item.Name).WithMaxItems(2);
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("1", bag.GetProperty(nameof(CollectionItem.Value), "General.One").Value);
        Assert.Equal("2", bag.GetProperty(nameof(CollectionItem.Value), "General.Two").Value);
        Assert.DoesNotContain(bag.Categories, category => category.Name == "Three");
    }

    [Fact]
    public void SectionByItemDoesNotHideNamedCollectionDrillDown()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).SectionByItem(item => item.Name.Length);
            type.Property("Item things", sample => sample.Items).Category("Items").AsDrillDown();
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());
        Property property = bag.GetProperty("Item things", "Items");

        Assert.NotNull(property);
        Assert.Equal("3", property.Value);
        Assert.True(property.CanDrillDown);
        Assert.Equal("1", bag.GetProperty(nameof(CollectionItem.Value), "General.3").Value);
    }

    [Fact]
    public void CollectionListUsesTypedSelectorsAndMaxItems()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items)
                .ListItems(list => list.Name(item => item.Name).Value(item => item.Value.ToString()))
                .WithMaxItems(2);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("1", bag.GetProperty("One", "General").Value);
        Assert.Equal("2", bag.GetProperty("Two", "General").Value);
        Assert.Null(bag.GetProperty("Three", "General"));
    }

    [Fact]
    public void CollectionListSupportsComputedNames()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items)
                .ListItems(list =>
                    list.Name(item => $"Item: {item.Name}")
                        .Value(item => item.Value.ToString())
                        .Description(item => $"Description: {item.Name}")
                        .Category(item => $"Group: {item.Name}")
                );
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Property property = bag.GetProperty("Item: One", "General.Group: One");
        Assert.Equal("1", property.Value);
        Assert.Equal("Description: One", property.Description);
    }

    [Fact]
    public void DrillDownUsesSeparateTypeConfiguration()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithDrillDown();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        configuration.ConfigureDrillDown<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Excluded);
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        RegisteredObject registered = new(root, "Tests", "Root");
        PropertyBag rootBag = DiagnosticManager.ObjectToPropertyBag(root, "Root", "Tests");
        Assert.True(rootBag.GetProperty(nameof(DrillDownRoot.Child), "General").CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { registered },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root|General|Child" } }
        );

        PropertyBag childBag = Assert.Single(response.Diagnostics.PropertyBags);
        Assert.Equal("Nested", childBag.GetProperty(nameof(ChildSample.Excluded), "General").Value);
        Assert.Null(childBag.GetProperty(nameof(ChildSample.Name), "General"));
    }

    [Fact]
    public void DrillDownResolvesStaticAndInstanceEventViews()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithDrillDown();
        });
        configuration.ConfigureDrillDown<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Route("Widgets", LoggerNameMatchMode.Prefix, route => route.To("Events", "All Widgets"));
            type.Route(child => child.LoggerName, LoggerNameMatchMode.Exact, route => route.To("Events", "Selected Widget"));
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(new DrillDownRoot(), "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root|General|Child" } }
        );

        Assert.Collection(
            response.EventViews,
            view =>
            {
                Assert.Equal(("Events", "All Widgets"), (view.Category, view.Name));
                DrillDownEventMatcher matcher = Assert.Single(view.Matchers);
                Assert.Equal(("Widgets", LoggerNameMatchMode.Prefix), (matcher.LoggerName, matcher.MatchMode));
            },
            view =>
            {
                Assert.Equal(("Events", "Selected Widget"), (view.Category, view.Name));
                DrillDownEventMatcher matcher = Assert.Single(view.Matchers);
                Assert.Equal(("Widgets.Nested", LoggerNameMatchMode.Exact), (matcher.LoggerName, matcher.MatchMode));
            }
        );
    }

    [Fact]
    public void DrillDownWithoutTypeConfigurationReturnsNoEventViews()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type => type.Property(sample => sample.Child).WithDrillDown());
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(new DrillDownRoot(), "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root|General|Child" } }
        );

        Assert.Null(response.ErrorMessage);
        Assert.Empty(response.EventViews);
    }

    [Fact]
    public void DrillDownFallsBackToNormalTypeConfiguration()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithDrillDown();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root|General|Child" } }
        );

        Assert.Equal("Nested", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(ChildSample.Name), "General").Value);
    }

    [Fact]
    public void DrillDownIconSuppressesDisplayValueButRetainsTarget()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).AsDrillDownIcon();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        PropertyBag bag = DiagnosticManager.ObjectToPropertyBag(root, "Root", "Tests");
        Property property = bag.GetProperty(nameof(DrillDownRoot.Child), "General");

        Assert.True(property.CanDrillDown);
        Assert.True(property.DrillDownIconOnly);
        Assert.Null(property.Value);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root|General|Child" } }
        );

        Assert.Equal("Nested", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(ChildSample.Name), "General").Value);
    }

    [Fact]
    public void ExplicitDrillDownProfileMakesRegisteredObjectBagDrillable()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        configuration.ConfigureDrillDown<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Excluded);
        });
        DiagnosticManager.UseConfiguration(configuration);

        ChildSample child = new();
        RegisteredObject registered = new(child, "Tests", "Child");
        PropertyBag bag = DiagnosticManager.ObjectToPropertyBag(child, "Child", "Tests");

        Assert.True(bag.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { registered },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Child" } }
        );

        PropertyBag childBag = Assert.Single(response.Diagnostics.PropertyBags);
        Assert.Equal("Nested", childBag.GetProperty(nameof(ChildSample.Excluded), "General").Value);
        Assert.Null(childBag.GetProperty(nameof(ChildSample.Name), "General"));
    }

    [Fact]
    public void CollectionListDrillsIntoItemRatherThanDisplayedValue()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items)
                .ListItems(list => list.Name(item => item.Name).Value(item => item.Value.ToString()))
                .AsDrillDownIcon();
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        CollectionSample sample = new();
        PropertyBag bag = DiagnosticManager.ObjectToPropertyBag(sample, "Collection", "Tests");
        Assert.True(bag.GetProperty("One", "General").CanDrillDown);
        Assert.True(bag.GetProperty("One", "General").DrillDownIconOnly);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection|General|One" } }
        );

        Assert.Equal("1", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(CollectionItem.Value), "General").Value);
    }

    [Fact]
    public void EnumerableDrillDownUsesPerPropertyLimit()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false, DrillDownMaxItems = 10 };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).WithDrillDown(maxItems: 2);
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        CollectionSample sample = new();
        PropertyBag bag = Render(sample);
        Property property = bag.GetProperty("Items", "General");
        Assert.Equal("3", property.Value);
        Assert.True(property.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection|General|Items" } }
        );

        Assert.Equal(2, response.DisplayedCount);
        Assert.Equal(3, response.TotalCount);
        Assert.True(response.IsTruncated);
        Assert.Equal(new[] { "[0]", "[1]" }, response.Diagnostics.PropertyBags.Select(bag => bag.Name));
    }

    [Fact]
    public void NamedDelegateCollectionDefaultsToCountAndSupportsDrillDown()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false, DrillDownMaxItems = 10 };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property("Item inventory", sample => sample.Items).AsDrillDown(maxItems: 2);
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        CollectionSample sample = new();
        PropertyBag bag = Render(sample);
        Property property = bag.GetProperty("Item inventory", "General");
        Assert.Equal("3", property.Value);
        Assert.True(property.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection|General|Item inventory" } }
        );

        Assert.Equal(2, response.DisplayedCount);
        Assert.Equal(3, response.TotalCount);
        Assert.True(response.IsTruncated);
    }

    [Fact]
    public void NestedDrillDownUsesAnOrderedPathChain()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithDrillDown();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Property(child => child.Details).WithDrillDown();
        });
        configuration.Configure<GrandChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest
            {
                ObjectPaths = new List<string> { "Tests|Root|General|Child", "DrillDown|ChildSample|General|Details" },
            }
        );

        Assert.Equal("Deep", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(GrandChildSample.Value), "General").Value);
    }

    [Fact]
    public void SetPropertyUsesDrillDownContextBeforeTheExistingPropertyPath()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithDrillDown();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Property(child => child.Editable).AllowSet();
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        OperationResponse response = DiagnosticManager.SetProperty(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new SetPropertyRequest
            {
                ObjectPaths = new[] { "Tests|Root|General|Child" },
                Path = "DrillDown|ChildSample|General|Editable",
                Value = "Changed",
            }
        );

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal("Changed", root.Child.Editable);
    }

    [Fact]
    public void FluentStrategiesWorkWithoutSourceAttributesAndApplyToNestedTypes()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Requests).Named("Requests").ShowRate(false).ShowTotal();
            type.Property(sample => sample.Started).ShowDate(false).ShowElapsed();
            type.Property(sample => sample.Details).Named("Details").Expand();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        StrategySample sample = new();
        sample.Requests.Register(4);
        PropertyBag bag = Render(sample);

        Assert.Equal("4", bag.GetProperty("Total Requests", "General").Value);
        Assert.Null(bag.GetProperty("Requests/sec", "General"));
        Assert.NotNull(bag.GetProperty("Time since Started", "General"));
        Assert.Null(bag.GetProperty(nameof(StrategySample.Started), "General"));
        Assert.Equal("Nested", bag.GetProperty(nameof(ChildSample.Name), "Details").Value);
    }

    [Fact]
    public void DatePropertyOverloadsInferDateStrategy()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            StrategySample.ConfigurePrivateDateProperty(type);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new StrategySample());

        Assert.NotNull(bag.GetProperty("Time since _privateStarted", "General"));
        Assert.NotNull(bag.GetProperty("Time since Last updated", "General"));
    }

    [Fact]
    public void RatePropertyOverloadsInferRateStrategy()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            StrategySample.ConfigurePrivateRateProperty(type);
        });
        DiagnosticManager.UseConfiguration(configuration);

        StrategySample sample = new();
        sample._privateRequests.Register(4);
        PropertyBag bag = Render(sample);

        Assert.Equal("4", bag.GetProperty("Total _privateRequests", "General").Value);
        Assert.Equal("4", bag.GetProperty("Total Background requests", "General").Value);
    }

    [Fact]
    public void FluentStrategiesOverrideSpecializedAttributesAndPreserveTheirMetadata()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributedStrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ConcatItems(", ").WithMaxItems(2);
            type.Property(sample => sample.Requests).ShowRate(false).ShowTotal();
            type.Property(sample => sample.Started).ShowDate(false).ShowElapsed();
            type.Property(sample => sample.Details).Named("Configured details").Expand();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        AttributedStrategySample sample = new();
        sample.Requests.Register(6);
        PropertyBag bag = Render(sample);

        Assert.Equal("3 items: One, Two, ... (1 more item)", bag.GetProperty("Attributed items", "Collections").Value);
        Assert.Equal("6", bag.GetProperty("Total Attributed requests", "Metrics").Value);
        Assert.Null(bag.GetProperty("Attributed requests/sec", "Metrics"));
        Assert.NotNull(bag.GetProperty("Time since Attributed started", "Timing"));
        Assert.Null(bag.GetProperty("Attributed started", "Timing"));
        Assert.Equal("Nested", bag.GetProperty(nameof(ChildSample.Name), "Configured details").Value);
    }

    [Fact]
    public void BaseConfigurationFlowsToDerivedTypesAndDerivedRulesWin()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<BaseSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.BaseValue).Named("Base configured");
        });
        configuration.Configure<DerivedSample>(type =>
        {
            type.Include(sample => sample.DerivedValue);
            type.Property(sample => sample.BaseValue).Named("Derived override");
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new DerivedSample());

        Assert.Equal("Base", bag.GetProperty("Derived override", "General").Value);
        Assert.Equal("Derived", bag.GetProperty(nameof(DerivedSample.DerivedValue), "General").Value);
        Assert.Null(bag.GetProperty("Base configured", "General"));
    }

    [Fact]
    public void ReplacingConfigurationInvalidatesGetterCacheWithoutMergingRules()
    {
        DiagnosticConfiguration first = new();
        first.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Include(sample => sample.First);
        });
        DiagnosticManager.UseConfiguration(first);
        Assert.NotNull(Render(new ReplacementSample()).GetProperty(nameof(ReplacementSample.First), "General"));

        DiagnosticConfiguration second = new();
        second.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Include(sample => sample.Second);
        });
        DiagnosticManager.UseConfiguration(second);
        PropertyBag bag = Render(new ReplacementSample());

        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), "General"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), "General"));
    }

    [Fact]
    public void HostingExtensionInstallsFluentConfiguration()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();

        services.ConfigureDiagnosticExplorer(diagnostics =>
            diagnostics.Configure<ReplacementSample>(type =>
            {
                type.ExcludeAll();
                type.Include(sample => sample.Second);
            })
        );

        PropertyBag bag = Render(new ReplacementSample());
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), "General"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), "General"));
    }

    [Fact]
    public void HostingExtensionDefersFluentConfigurationUntilDiagnosticsAreRequested()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();
        bool configured = false;

        services.ConfigureDiagnosticExplorer(
            hostConfiguration,
            diagnostics =>
            {
                diagnostics.Configure<ReplacementSample>(type =>
                {
                    configured = true;
                    type.ExcludeAll();
                    type.Include(sample => sample.Second);
                });
            }
        );

        Assert.False(configured);

        PropertyBag bag = Render(new ReplacementSample());

        Assert.True(configured);
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), "General"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), "General"));
    }

    [Fact]
    public void HostingExtensionDiscoversConfiguredObjectsOnFirstDiagnosticsRequest()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();
        ReplacementSample sample = new();
        services.AddSingleton(sample);
        services.ConfigureDiagnosticExplorer(
            hostConfiguration,
            diagnostics =>
            {
                diagnostics.RegisterObjects(registrations => registrations.RegisterService<ReplacementSample>("Application", "Sample"));
                diagnostics.Configure<ReplacementSample>(type => type.IncludeAll());
            }
        );
        using ServiceProvider provider = services.BuildServiceProvider();

        DiagnosticResponse response = DiagnosticManager.GetDiagnostics(provider);

        PropertyBag bag = Assert.Single(response.PropertyBags.Where(item => item.Name == "Sample"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), "General"));
    }

    [Fact]
    public void HostingExtensionAppliesRuntimeRoutesBeforeDiagnosticsAreRequested()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();
        bool configured = false;

        services.ConfigureDiagnosticExplorer(
            hostConfiguration,
            diagnostics =>
            {
                diagnostics.ConfigureEventRouting(routes => routes.Route("Widgets", route => route.To("Application", "Widget Events")));
                diagnostics.Configure<ReplacementSample>(_ => configured = true);
            }
        );

        LogStreamRoutingConfiguration routing = GetReplayRouting(DiagnosticManager.LogEventStore);

        Assert.False(configured);
        Assert.Equal("Widgets", Assert.Single(routing.Routes).LoggerName);
        Assert.Equal("Application", Assert.Single(routing.Routes[0].Destinations).Category.Value);
    }

    [Fact]
    public void HostingExtensionDoesNotConfigureDiagnosticsWhenDisabled()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["DiagnosticExplorer:Enabled"] = "false" })
            .Build();
        bool configured = false;

        services.ConfigureDiagnosticExplorer(hostConfiguration, _ => configured = true);

        Assert.False(DiagnosticManager.Enabled);
        Assert.False(configured);
        _ = Render(new ReplacementSample());
        Assert.False(configured);
    }

    [Fact]
    public void DeferredConfigurationFailureDoesNotEscapeOrDisableDiagnostics()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();

        services.ConfigureDiagnosticExplorer(
            hostConfiguration,
            diagnostics => diagnostics.Configure<ReplacementSample>(_ => throw new InvalidOperationException("Configuration failed"))
        );

        Exception exception = Record.Exception(() => Render(new ReplacementSample()));

        Assert.Null(exception);
        Assert.True(DiagnosticManager.Enabled);
    }

    [Fact]
    public void DeferredConfigurationIgnoresInvalidSelectorsAndAppliesRemainingSelectors()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();

        services.ConfigureDiagnosticExplorer(
            hostConfiguration,
            diagnostics =>
                diagnostics.Configure<ReplacementSample>(type =>
                {
                    type.ExcludeAll();
                    type.Property(sample => sample.First.Length).Named("Invalid nested selector");
                    type.Property(sample => sample.Second);
                })
        );

        PropertyBag bag = Render(new ReplacementSample());

        Assert.True(DiagnosticManager.Enabled);
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), "General"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), "General"));
    }

    [Fact]
    public void ConfiguredObjectProvidersAreEvaluatedForEachRequest()
    {
        ReplacementSample first = new();
        ReplacementSample second = new();
        ReplacementSample discovered = first;
        DiagnosticConfiguration configuration = new();
        IServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        configuration.RegisterObjects(registrations =>
        {
            Assert.Same(serviceProvider.GetService(typeof(IServiceProvider)), registrations.GetService(typeof(IServiceProvider)));
            registrations.Register(discovered, "Configured", discovered == first ? "First" : "Second");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Assert.Contains(DiagnosticManager.GetRegisteredObjects(serviceProvider), item => ReferenceEquals(item.Object, first));

        discovered = second;

        RegisteredObject registered = Assert.Single(
            DiagnosticManager.GetRegisteredObjects(serviceProvider).Where(item => ReferenceEquals(item.Object, second))
        );
        Assert.Equal("Configured", registered.BagCategory);
        Assert.Equal("Second", registered.BagName);
        Assert.DoesNotContain(DiagnosticManager.GetRegisteredObjects(serviceProvider), item => ReferenceEquals(item.Object, first));
    }

    [Fact]
    public void ExplicitRegistrationTakesPrecedenceOverConfiguredDiscovery()
    {
        ReplacementSample sample = new();
        DiagnosticConfiguration configuration = new();
        configuration.RegisterObjects(registrations => registrations.Register(sample, "Discovered", "Discovered"));
        DiagnosticManager.UseConfiguration(configuration);
        DiagnosticManager.Register(sample, "Explicit", "Registered");

        try
        {
            RegisteredObject registered = Assert.Single(DiagnosticManager.GetRegisteredObjects().Where(item => ReferenceEquals(item.Object, sample)));
            Assert.Equal("Registered", registered.BagCategory);
            Assert.Equal("Explicit", registered.BagName);
        }
        finally
        {
            DiagnosticManager.Unregister(sample);
        }
    }

    [Fact]
    public void InvalidExpressionsAndLimitsAreRejected()
    {
        DiagnosticConfiguration configuration = new();

        Assert.Throws<ArgumentException>(() => configuration.Configure<StrategySample>(type => type.Property(sample => sample.Details.Name)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            configuration.Configure<CollectionSample>(type => type.Property(sample => sample.Items).WithMaxItems(0))
        );
    }

    private static PropertyBag Render(object value)
    {
        return DiagnosticManager.ObjectToPropertyBag(value, "Test", "Tests");
    }

    private static LogStreamRoutingConfiguration GetReplayRouting(LogEventStore store)
    {
        using LogEventStore.LogEventStoreSubscription subscription = store.CreateSubscription();
        return subscription.Initialization.Routing;
    }

    [DiagnosticClass(AttributedPropertiesOnly = true)]
    private sealed class AttributeSample
    {
        [DiagnosticProperty("Attributed name", Category = "Source", Description = "Source description", FormatString = "{0:D4}", AllowSet = true)]
        public int Attributed { get; set; } = 7;

        [DiagnosticProperty(Ignore = true)]
        public string Ignored { get; set; } = "Ignored";

        public string Hidden { get; set; } = "Hidden";

        public string Plain { get; set; } = "Plain";
    }

    private sealed class DefaultScalarSample
    {
        public string Text { get; } = "Text";
        public DefaultScalarStatus Status { get; } = DefaultScalarStatus.Ready;
        public int Count { get; } = 1;
        public int? OptionalCount { get; } = 2;
        public decimal Amount { get; } = 3.5m;
        public DateTime Created { get; } = new(2025, 1, 2);
        public TimeSpan Duration { get; } = TimeSpan.FromMinutes(1);
        public ChildSample Child { get; } = new();
        public IReadOnlyList<string> Items { get; } = new[] { "One", "Two" };
        public System.Threading.Tasks.Task Pending { get; } = System.Threading.Tasks.Task.CompletedTask;
    }

    private enum DefaultScalarStatus
    {
        Ready,
    }

    private sealed class ValueKindSample
    {
        public string Text { get; } = "Text";
        public int Positive { get; } = 1;
        public int Zero { get; }
        public int Negative { get; } = -1;
        public DateTime Timestamp { get; } = new(2025, 1, 2);
        public TimeSpan Duration { get; } = TimeSpan.FromMinutes(1);
        public bool Enabled { get; } = true;
        public DefaultScalarStatus Status { get; } = DefaultScalarStatus.Ready;
        public object Details { get; } = new();
    }

    private sealed class CollectionSample
    {
        public ICollection<CollectionItem> Items { get; } = new List<CollectionItem> { new("One", 1), new("Two", 2), new("Three", 3) };
        private readonly ICollection<CollectionItem> _privateItems = new List<CollectionItem> { new("One", 1), new("Two", 2) };

        public static void ConfigurePrivateItems(ITypeConfigurator<CollectionSample> type)
        {
            type.Property("Private items", sample => sample._privateItems)
                .ListItems(list => list.Name(item => item.Name).Value(item => item.Value.ToString()));
        }

        public static void ConfigurePrivateItemsFromField(ITypeConfigurator<CollectionSample> type)
        {
            type.Property(sample => sample._privateItems).ListItems(list => list.Name(item => item.Name).Value(item => item.Value.ToString()));
        }
    }

    private sealed class CollectionInterfaceSample
    {
        public CollectionItem[] Array { get; } = { new("Array", 1) };
        public IList<CollectionItem> List { get; } = new List<CollectionItem> { new("List", 1) };
        public IReadOnlyList<CollectionItem> ReadOnlyList { get; } = new List<CollectionItem> { new("Read-only list", 1) };
        public ICollection<CollectionItem> Collection { get; } = new List<CollectionItem> { new("Collection", 1) };
        public IReadOnlyCollection<CollectionItem> ReadOnlyCollection { get; } = new List<CollectionItem> { new("Read-only collection", 1) };
        public ISet<CollectionItem> Set { get; } = new HashSet<CollectionItem> { new("Set", 1) };
    }

    private sealed class CollectionItem
    {
        public CollectionItem(string name, int value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public int Value { get; }

        public override string ToString() => Name;
    }

    private sealed class PrivatePropertySample
    {
        internal readonly string _value = "Value";
    }

    private sealed class JsonFormatSample
    {
        public JsonFormatDetails Details { get; } = new();
    }

    private sealed class JsonFormatDetails
    {
        public string Value { get; } = "JSON";
    }

    private sealed class StrategySample
    {
        public RateCounter Requests { get; } = new(5);
        internal readonly RateCounter _privateRequests = new(5);
        public DateTime Started { get; } = DateTime.UtcNow.AddMinutes(-1);
        private readonly DateTime _privateStarted = DateTime.UtcNow.AddMinutes(-2);
        public ChildSample Details { get; } = new();
        private readonly ChildSample _privateDetails = new();

        public static void ConfigurePrivateDetails(ITypeConfigurator<StrategySample> type)
        {
            type.Property("Private details", sample => sample._privateDetails).Expand();
        }

        public static void ConfigurePrivateDetailsFromField(ITypeConfigurator<StrategySample> type)
        {
            type.Property(sample => sample._privateDetails).Expand();
        }

        public static void ConfigurePrivateDateProperty(ITypeConfigurator<StrategySample> type)
        {
            type.Property(sample => sample._privateStarted).ShowDate(false).ShowElapsed();
            type.Property("Last updated", sample => sample._privateStarted).ShowDate(false).ShowElapsed();
        }

        public static void ConfigurePrivateRateProperty(ITypeConfigurator<StrategySample> type)
        {
            type.Property(sample => sample._privateRequests).ShowRate(false).ShowTotal();
            type.Property("Background requests", sample => sample._privateRequests).ShowRate(false).ShowTotal();
        }
    }

    private sealed class AttributedStrategySample
    {
        [CollectionCount("Attributed items", "Collections")]
        public ICollection<CollectionItem> Items { get; } = new List<CollectionItem> { new("One", 1), new("Two", 2), new("Three", 3) };

        [RateProperty("Attributed requests", "Metrics", ExposeRate = true, ExposeTotal = false)]
        public RateCounter Requests { get; } = new(5);

        [DateProperty("Attributed started", "Timing", ExposeDate = true, ExposeElapsed = false)]
        public DateTime Started { get; } = DateTime.UtcNow.AddMinutes(-1);

        [ExtendedProperty("Attributed details")]
        public ChildSample Details { get; } = new();
    }

    private sealed class ChildSample
    {
        public string Name { get; } = "Nested";
        public string LoggerName { get; } = "Widgets.Nested";
        public string Excluded { get; } = "Nested";
        public GrandChildSample Details { get; } = new();
        public string Editable { get; set; } = "Initial";
    }

    private sealed class DrillDownRoot
    {
        public ChildSample Child { get; } = new();
        public ICollection<CollectionItem> Items { get; } = new List<CollectionItem> { new("One", 1) };
        public RateCounter Requests { get; } = new(5);
    }

    private sealed class GrandChildSample
    {
        public string Value { get; } = "Deep";
    }

    private class BaseSample
    {
        public string BaseValue { get; } = "Base";
    }

    private sealed class DerivedSample : BaseSample
    {
        public string DerivedValue { get; } = "Derived";
    }

    private sealed class ReplacementSample
    {
        public string First { get; } = "First";
        public string Second { get; } = "Second";
    }

    private sealed class DefaultFormatSample
    {
        public DateTime Value { get; } = new(2025, 1, 2, 3, 4, 5);
        public DateTime? NullableValue { get; } = new(2025, 1, 2, 3, 4, 5);
    }

    private sealed class WarningSample
    {
        public WarningSample(int current, int minimum)
        {
            Current = current;
            Minimum = minimum;
        }

        public int Current { get; }
        public int Minimum { get; }
    }
}
