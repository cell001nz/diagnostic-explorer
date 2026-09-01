using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        Property property = bag.GetProperty(nameof(AttributeSample.Attributed), null);
        Assert.NotNull(property);
        Assert.Equal("7", property.Value);
        Assert.Null(property.Description);
        Assert.False(property.CanSet);
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Ignored), null));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Hidden), null));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Plain), null));
        Assert.DoesNotContain(bag.Categories, category => category.Name == "Source");
        Assert.All(bag.Categories.Single(category => category.Name == null).Properties, item => Assert.NotNull(item));
    }

    [Fact]
    public void EmptyConfigurationUsesUsefulPropertyDefaults()
    {
        PropertyBag bag = Render(new DefaultScalarSample());

        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Text), null));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Status), null));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Count), null));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.OptionalCount), null));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Amount), null));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Created), null));
        Assert.NotNull(bag.GetProperty(nameof(DefaultScalarSample.Duration), null));
        Property point = bag.GetProperty(nameof(DefaultScalarSample.Point), null);
        Assert.False(point.CanDrillDown);
        Assert.False(point.DrillDownIconOnly);
        Assert.Equal("(3, 4)", point.Value);
        Property child = bag.GetProperty(nameof(DefaultScalarSample.Child), null);
        Assert.True(child.CanDrillDown);
        Assert.True(child.DrillDownIconOnly);
        Assert.Null(child.Value);
        Assert.Equal("2", bag.GetProperty(nameof(DefaultScalarSample.Items), null).Value);
        Assert.Null(bag.GetProperty(nameof(DefaultScalarSample.Pending), null));
    }

    [Fact]
    public void RenderedPropertiesExposeValueKinds()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<ValueKindSample>(type => type.IncludeAll());
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ValueKindSample());

        Assert.Equal(PropertyValueKind.Text, bag.GetProperty(nameof(ValueKindSample.Text), null).ValueKind);
        Assert.Equal(PropertyValueKind.PositiveNumber, bag.GetProperty(nameof(ValueKindSample.Positive), null).ValueKind);
        Assert.Equal(PropertyValueKind.ZeroNumber, bag.GetProperty(nameof(ValueKindSample.Zero), null).ValueKind);
        Assert.Equal(PropertyValueKind.NegativeNumber, bag.GetProperty(nameof(ValueKindSample.Negative), null).ValueKind);
        Assert.Equal(PropertyValueKind.DateTime, bag.GetProperty(nameof(ValueKindSample.Timestamp), null).ValueKind);
        Assert.Equal(PropertyValueKind.Duration, bag.GetProperty(nameof(ValueKindSample.Duration), null).ValueKind);
        Assert.Equal(PropertyValueKind.Boolean, bag.GetProperty(nameof(ValueKindSample.Enabled), null).ValueKind);
        Assert.Equal(PropertyValueKind.Enumeration, bag.GetProperty(nameof(ValueKindSample.Status), null).ValueKind);
        Assert.Equal(PropertyValueKind.Object, bag.GetProperty(nameof(ValueKindSample.Details), null).ValueKind);
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

        Assert.Equal("2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.Value), null).Value);
        Assert.Equal("2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.NullableValue), null).Value);

        configuration.DefaultFormat<DateTime>("The date is {0:d MMM yyyy HH:mm:ss}");
        DiagnosticManager.UseConfiguration(configuration);

        bag = Render(new DefaultFormatSample());

        Assert.Equal("The date is 2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.Value), null).Value);
        Assert.Equal("The date is 2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.NullableValue), null).Value);
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
    public void FluentPropertyCanOverrideDisplayTextWithoutHidingStatuses()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.First).WithText("").Status(StatusCode.Running, _ => true);
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new ReplacementSample()).GetProperty(nameof(ReplacementSample.First), null);

        Assert.Equal(string.Empty, property.Value);
        PropertyStatus status = Assert.Single(property.Statuses);
        Assert.Equal(StatusCode.Running, status.Status);
    }

    [Fact]
    public void FluentPropertyCanUseOwnerBasedDisplayText()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.First).WithText(sample => $"Current: {sample.Second}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new ReplacementSample()).GetProperty(nameof(ReplacementSample.First), null);

        Assert.Equal("Current: Second", property.Value);
    }

    [Fact]
    public void FluentPropertyCanSelectStatusIconSize()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.First).WithIconSize(StatusIconSize.Large);
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new ReplacementSample()).GetProperty(nameof(ReplacementSample.First), null);

        Assert.Equal(StatusIconSize.Large, property.StatusIconSize);
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

        Property property = Render(new JsonFormatSample()).GetProperty(nameof(JsonFormatSample.Details), null);

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

        Property property = Render(new DefaultFormatSample()).GetProperty(nameof(DefaultFormatSample.Value), null);

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

        Property property = Render(new JsonFormatSample()).GetProperty(nameof(JsonFormatSample.Details), null);

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
                .WithLabel(sample => $"Value {sample.Attributed}")
                .WithCategory(sample => $"Source {sample.Attributed}")
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

        Property warningProperty = Render(new WarningSample(3, 5)).GetProperty(nameof(WarningSample.Current), null);
        Property errorProperty = Render(new WarningSample(-1, 5)).GetProperty(nameof(WarningSample.Current), null);
        Property validProperty = Render(new WarningSample(5, 3)).GetProperty(nameof(WarningSample.Current), null);

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
                type.Property(sample => sample.Second).WithCategory("Specific");
            }
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ReplacementSample());

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), "Scoped"));
        Assert.NotNull(bag.GetProperty("Computed", "Scoped"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), "Specific"));
    }

    [Fact]
    public void CategoryScopeCanExpandItsSubBagByDefault()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            using (type.CreateCategoryScope("Scoped").Expanded())
                type.Property(sample => sample.First);
        });
        DiagnosticManager.UseConfiguration(configuration);

        Category category = Render(new ReplacementSample()).Categories.FindByName("Scoped");

        Assert.True(category.IsExpanded);
        Assert.False(category.IsExpandedProperty);
    }

    [Fact]
    public void NamedPropertyUsesDelegateAndFluentMetadata()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property("Computed", sample => sample.First + sample.Second)
                .WithCategory(sample => $"Calculated {sample.First}")
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
    public void AlertsCanUseDefaultMessages()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.First).Warn(sample => sample.First == "First").Error(sample => sample.Second == "Second");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new ReplacementSample()).GetProperty(nameof(ReplacementSample.First), null);

        Assert.Collection(
            property.Alerts,
            alert =>
            {
                Assert.Equal(PropertyAlertSeverity.Warning, alert.Severity);
                Assert.Equal("Warning", alert.Message);
                Assert.Equal("Warning", alert.Category);
            },
            alert =>
            {
                Assert.Equal(PropertyAlertSeverity.Error, alert.Severity);
                Assert.Equal("Error", alert.Message);
                Assert.Equal("Error", alert.Category);
            }
        );
    }

    [Fact]
    public void PropertiesCanRenderMultipleStatuses()
    {
        ReplacementSample sample = new();
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Property(item => item.First)
                .Status(StatusCode.Active, item => item.First == "First")
                .Status(StatusCode.Success, item => item.Second == "Second", item => $"Ready: {item.Second}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(sample).GetProperty(nameof(ReplacementSample.First), null);

        Assert.Collection(
            property.Statuses,
            status =>
            {
                Assert.Equal(StatusCode.Active, status.Status);
                Assert.Equal("Active", status.Text);
            },
            status =>
            {
                Assert.Equal(StatusCode.Success, status.Status);
                Assert.Equal("Ready: Second", status.Text);
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

        Property property = Render(new ReplacementSample()).GetProperty("Computed", null);

        Assert.Equal("\"FirstSecond\"", property.Value);
    }

    [Fact]
    public void NamedPropertyCanRenderWithDrillDownOnly()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property("Details", sample => sample.Details).WithDrillDownOnly("View more details");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new StrategySample()).GetProperty("Details", null);

        Assert.True(property.CanDrillDown);
        Assert.True(property.DrillDownIconOnly);
        Assert.Equal("View more details", property.DrillDownText);
        Assert.Null(property.Value);
    }

    [Fact]
    public void NamedPropertyCanRenderWithDynamicDrillDownOnlyText()
    {
        StrategySample sample = new();
        DiagnosticConfiguration configuration = new();
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property("Details", item => item.Details).WithDrillDownOnly(item => $"View {item.GetHashCode()}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(sample).GetProperty("Details", null);

        Assert.Equal($"View {sample.GetHashCode()}", property.DrillDownText);
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
        Property property = Render(root).GetProperty(nameof(DrillDownRoot.Child), null);

        Assert.True(property.CanJsonHover);
        Assert.False(property.CanExpandedHover);
        Assert.False(property.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest
            {
                ObjectPaths = new List<string> { "Tests|Root||Child" },
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
        Property property = Render(root).GetProperty(nameof(DrillDownRoot.Child), null);

        Assert.False(property.CanJsonHover);
        Assert.True(property.CanExpandedHover);
        Assert.False(property.CanDrillDown);

        DrillDownResponse normalResponse = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Child" } }
        );
        Assert.NotEmpty(normalResponse.EventViews);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest
            {
                ObjectPaths = new List<string> { "Tests|Root||Child" },
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
                        projection.Property(root => root.Child).Expand();
                        projection
                            .Property("Items", root => root.Items)
                            .ListItems()
                            .WithListItemName(item => item.Name)
                            .WithListItemValue(item => item.Value.ToString());
                        projection.Property("Requests", root => root.Requests).ShowRate(false).ShowTotal();
                    }
                )
                .WithDrillDownOnly()
        );
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        root.Requests.Register(5);
        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Summary" } }
        );

        PropertyBag bag = Assert.Single(response.Diagnostics.PropertyBags);
        Assert.Equal("Projection", bag.GetProperty("Name", null).Value);
        Assert.Null(bag.GetProperty("Child", null));
        Assert.NotNull(bag.GetProperty(nameof(ChildSample.Name), "Child"));
        Assert.Equal("1", bag.GetProperty("One", null).Value);
        Assert.Equal("5", bag.GetProperty("Total Requests", null).Value);
    }

    [Fact]
    public void InlineCustomProjectionCanRenderWithDrillDown()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
            type.Custom("Summary", projection => projection.Property("Name", root => "Projection")).WithDrillDown()
        );
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        Property property = Render(root).GetProperty("Summary", null);

        Assert.True(property.CanDrillDown);
        Assert.False(property.DrillDownIconOnly);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Summary" } }
        );

        PropertyBag bag = Assert.Single(response.Diagnostics.PropertyBags);
        Assert.Equal("Projection", bag.GetProperty("Name", null).Value);
    }

    [Fact]
    public void InlineCustomProjectionCanExpand()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
            type.Custom("Summary", projection => projection.Property("Items", root => root.Items).ExpandItems(item => item.Name)).Expand()
        );
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new DrillDownRoot());

        Assert.Null(bag.GetProperty("Summary", null));
        Assert.Equal("1", bag.GetProperty(nameof(CollectionItem.Value), "Summary.One").Value);
        Assert.True(bag.Categories.FindByName("Summary").IsExpanded);
        Assert.True(bag.Categories.FindByName("Summary").IsExpandedProperty);
        Assert.Null(bag.Categories.FindByName("Summary.Items"));
    }

    [Fact]
    public void InlineCustomProjectionCanRenderDirectFields()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<PrivatePropertySample>(type =>
            type.Custom("Projection", projection => projection.Property(sample => sample._value)).WithDrillDown()
        );
        DiagnosticManager.UseConfiguration(configuration);

        PrivatePropertySample sample = new();
        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Projection" } }
        );

        PropertyBag bag = Assert.Single(response.Diagnostics.PropertyBags);
        Assert.Equal("Value", bag.GetProperty("_value", null).Value);
    }

    [Fact]
    public void FluentMetadataOverridesOnlyExplicitAttributeValues()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Attributed).WithLabel("Configured name").WithCategory("Configured");
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
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Ignored), null));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Hidden), null));
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

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), null));
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.Second), null));
    }

    [Fact]
    public void PropertiesWithoutCategoriesUseAnUnnamedCategory()
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

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), null));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), null));
        Assert.Null(Assert.Single(bag.Categories).Name);
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

        Assert.Equal("3", bag.GetProperty("Items count", null).Value);
        Assert.Equal("One, Two, ... (1 more item)", bag.GetProperty("Items", null).Value);
    }

    [Fact]
    public void CollectionCanFormatConcatenatedItems()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ConcatItems(" / ", item => $"{item.Name}:{item.Value}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("One:1 / Two:2 / Three:3", bag.GetProperty("Items", null).Value);
    }

    [Fact]
    public void CollectionCanWrapConcatenatedItemsWithoutChangingTheirFormatting()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ConcatItems(", ").WithMaxItems(2).WithTextWrap();
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Property property = bag.GetProperty("Items", null);
        Assert.Equal("One, Two, ... (1 more item)", property.Value);
        Assert.True(property.NoTruncate);
    }

    [Fact]
    public void CollectionOutputsConfigureExpandedHoverIndependently()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ListItems().WithExpandedHover().WithDrillDown();
            type.Property(sample => sample.Items).ConcatItems(", ").WithTextWrap().WithDrillDown();
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.True(bag.GetProperty("Items 0", null).CanExpandedHover);
        Assert.False(bag.GetProperty("Items", null).CanExpandedHover);
        Assert.True(bag.GetProperty("Items", null).NoTruncate);
    }

    [Fact]
    public void CollectionPresentationOutputsRetainPropertyMetadata()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items)
                .Description("Configured items")
                .Warn(_ => true, "Items need attention")
                .ShowCount()
                .ConcatItems(", ");
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());
        Property[] properties = { bag.GetProperty("Items count", null), bag.GetProperty("Items", null) };

        Assert.All(
            properties,
            property =>
            {
                Assert.Equal("Configured items", property.Description);
                PropertyAlert alert = Assert.Single(property.Alerts);
                Assert.Equal(PropertyAlertSeverity.Warning, alert.Severity);
                Assert.Equal("Items need attention", alert.Message);
            }
        );
    }

    [Fact]
    public void PropertyCollectionsSupportCommonGenericCollectionInterfaces()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionInterfaceSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Array).WithCategory("Array").ListItems().WithListItemName(item => item.Name);
            type.Property(sample => sample.List).WithCategory("List").ListItems().WithListItemName(item => item.Name);
            type.Property(sample => sample.ReadOnlyList).WithCategory("Read-only list").ListItems().WithListItemName(item => item.Name);
            type.Property(sample => sample.Collection).WithCategory("Collection").ListItems().WithListItemName(item => item.Name);
            type.Property(sample => sample.ReadOnlyCollection).WithCategory("Read-only collection").ListItems().WithListItemName(item => item.Name);
            type.Property(sample => sample.Set).WithCategory("Set").ListItems().WithListItemName(item => item.Name);
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
            type.Property(sample => sample.Array).ShowCount().ConcatItems(", ", item => item.Name).ExpandItems(item => item.Name).WithMaxItems(1);
            type.Property(sample => sample.List).ShowCount().ConcatItems(", ", item => item.Name).ExpandItems(item => item.Name).WithMaxItems(1);
            type.Property(sample => sample.ReadOnlyList)
                .ShowCount()
                .ConcatItems(", ", item => item.Name)
                .ExpandItems(item => item.Name)
                .WithMaxItems(1);
            type.Property(sample => sample.Collection)
                .ShowCount()
                .ConcatItems(", ", item => item.Name)
                .ExpandItems(item => item.Name)
                .WithMaxItems(1);
            type.Property(sample => sample.ReadOnlyCollection)
                .ShowCount()
                .ConcatItems(", ", item => item.Name)
                .ExpandItems(item => item.Name)
                .WithMaxItems(1);
            type.Property(sample => sample.Set).ShowCount().ConcatItems(", ", item => item.Name).ExpandItems(item => item.Name).WithMaxItems(1);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionInterfaceSample());

        Assert.Equal("Array", bag.GetProperty(nameof(CollectionInterfaceSample.Array), null).Value);
        Assert.Equal("List", bag.GetProperty(nameof(CollectionInterfaceSample.List), null).Value);
        Assert.Equal("Read-only list", bag.GetProperty(nameof(CollectionInterfaceSample.ReadOnlyList), null).Value);
        Assert.Equal("Collection", bag.GetProperty(nameof(CollectionInterfaceSample.Collection), null).Value);
        Assert.Equal("Read-only collection", bag.GetProperty(nameof(CollectionInterfaceSample.ReadOnlyCollection), null).Value);
        Assert.Equal("Set", bag.GetProperty(nameof(CollectionInterfaceSample.Set), null).Value);
    }

    [Fact]
    public void ConcreteCollectionDeclarationsSupportPresentationMethods()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ConcreteCollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.List).WithCategory("List").ListItems().WithListItemName(item => item.Name);
            type.Property(sample => sample.Set).WithCategory("Set").ShowCount();
            type.Property(sample => sample.Observable).WithCategory("Observable").ConcatItems(", ", item => item.Name);
            type.Property(sample => sample.Binding).WithCategory("Binding").ExpandItems(item => item.Name);
            type.Property(sample => sample.Dictionary).WithCategory("Dictionary").ListItems();
            type.Property(sample => sample.InterfaceDictionary)
                .WithCategory("Interface dictionary")
                .ConcatItems(", ", item => $"{item.Key}:{item.Value.Name}");
            type.Property(sample => sample.ReadOnlyDictionary).WithCategory("Read-only dictionary").ListItems();
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ConcreteCollectionSample());

        Assert.NotNull(bag.GetProperty("List", "List"));
        Assert.Equal("1", bag.GetProperty(nameof(ConcreteCollectionSample.Set), "Set").Value);
        Assert.Equal("Observable", bag.GetProperty(nameof(ConcreteCollectionSample.Observable), "Observable").Value);
        Assert.NotNull(bag.GetProperty(nameof(CollectionItem.Name), "Binding.Binding.Binding"));
        Assert.Equal("Dictionary", bag.GetProperty("First", "Dictionary").Value);
        Assert.Equal(
            "Second:Interface dictionary",
            bag.GetProperty(nameof(ConcreteCollectionSample.InterfaceDictionary), "Interface dictionary").Value
        );
        Assert.Equal("Read-only dictionary", bag.GetProperty("Third", "Read-only dictionary").Value);
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

        Assert.Equal("1", collectionBag.GetProperty("One", null).Value);
        Assert.Equal("2", collectionBag.GetProperty("Two", null).Value);
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

        Assert.Equal("1", collectionBag.GetProperty("One", null).Value);
        Assert.Equal("2", collectionBag.GetProperty("Two", null).Value);
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

        Assert.Equal("Value", bag.GetProperty("_value", null).Value);
        Assert.Equal("Private value", bag.GetProperty("_value", null).Description);
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

        Assert.Equal("4", bag.GetProperty("Total _privateRequests", null).Value);
        Assert.Null(bag.GetProperty("_privateRequests/sec", null));
    }

    [Fact]
    public void ExpandItemsUsesTypedSelectorAndMaxItems()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ExpandItems(item => item.Name).WithMaxItems(2);
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.True(bag.Categories.FindByName("Items").IsExpanded);
        Assert.Equal("1", bag.GetProperty(nameof(CollectionItem.Value), "Items.One").Value);
        Assert.Equal("2", bag.GetProperty(nameof(CollectionItem.Value), "Items.Two").Value);
        Assert.DoesNotContain(bag.Categories, category => category.Name == "Items.Three");
        Assert.Equal("1 more item", bag.GetProperty("Items (more)", "Items").Value);
    }

    [Fact]
    public void ExpandItemsCanStartCollapsed()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
            type.Property(sample => sample.Items).ExpandItems(item => item.Name, initiallyExpanded: false)
        );
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());
        Category category = bag.Categories.FindByName("Items");

        Assert.False(category.IsExpanded);
        Assert.True(category.IsExpandedProperty);
    }

    [Fact]
    public void ExpandItemsCanLimitItemPropertiesToPrimaryCategory()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
            type.Property(sample => sample.Items).ExpandItems(item => item.Name).WithPrimaryPropertiesOnly()
        );
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Name);
            type.Property(item => item.Value).WithCategory("Details");
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("One", bag.GetProperty(nameof(CollectionItem.Name), "Items.One").Value);
        Assert.Null(bag.GetProperty(nameof(CollectionItem.Value), "Items.One.Details"));
    }

    [Fact]
    public void ExpandItemsDoesNotHideNamedCollectionDrillDown()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ExpandItems(item => item.Name);
            type.Property("Item things", sample => sample.Items).WithCategory("Items").WithDrillDown();
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
        Assert.Equal("1", bag.GetProperty(nameof(CollectionItem.Value), "Items.One").Value);
    }

    [Fact]
    public void CollectionListUsesTypedSelectorsAndMaxItems()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items)
                .ListItems()
                .WithListItemName(item => item.Name)
                .WithListItemValue(item => item.Value.ToString())
                .WithMaxItems(2);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("1", bag.GetProperty("One", null).Value);
        Assert.Equal("2", bag.GetProperty("Two", null).Value);
        Assert.Null(bag.GetProperty("Three", null));
        Assert.Equal("1 more item", bag.GetProperty("Items (more)", null).Value);
    }

    [Fact]
    public void CollectionListSupportsComputedNames()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items)
                .ListItems()
                .WithListItemName(item => $"Item: {item.Name}")
                .WithListItemValue(item => item.Value.ToString())
                .WithListItemDescription(item => $"Description: {item.Name}")
                .WithListItemCategory(item => $"Group: {item.Name}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Property property = bag.GetProperty("Item: One", "Group: One");
        Assert.Equal("1", property.Value);
        Assert.Equal("Description: One", property.Description);
    }

    [Fact]
    public void ListItemFieldsRequireListItemsOutput()
    {
        DiagnosticConfiguration configuration = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.Configure<CollectionSample>(type =>
                type.Property(sample => sample.Items).ConcatItems(", ").WithListItemName(item => item.Name)
            )
        );

        Assert.Equal("ListItems must be configured before setting list item fields.", exception.Message);
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
        Assert.True(rootBag.GetProperty(nameof(DrillDownRoot.Child), null).CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { registered },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Child" } }
        );

        PropertyBag childBag = Assert.Single(response.Diagnostics.PropertyBags);
        Assert.Equal("Nested", childBag.GetProperty(nameof(ChildSample.Excluded), null).Value);
        Assert.Equal("Nested", childBag.GetProperty(nameof(ChildSample.Name), null).Value);
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
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Child" } }
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
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Child" } }
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
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Child" } }
        );

        Assert.Equal("Nested", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(ChildSample.Name), null).Value);
    }

    [Fact]
    public void DrillDownIconSuppressesDisplayValueButRetainsTarget()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<DrillDownRoot>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Child).WithDrillDownOnly();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(child => child.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        DrillDownRoot root = new();
        PropertyBag bag = DiagnosticManager.ObjectToPropertyBag(root, "Root", "Tests");
        Property property = bag.GetProperty(nameof(DrillDownRoot.Child), null);

        Assert.True(property.CanDrillDown);
        Assert.True(property.DrillDownIconOnly);
        Assert.Null(property.Value);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(root, "Tests", "Root") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Root||Child" } }
        );

        Assert.Equal("Nested", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(ChildSample.Name), null).Value);
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
        Assert.Equal("Nested", childBag.GetProperty(nameof(ChildSample.Excluded), null).Value);
        Assert.Equal("Nested", childBag.GetProperty(nameof(ChildSample.Name), null).Value);
    }

    [Fact]
    public void CollectionListDrillsIntoItemRatherThanDisplayedValue()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items)
                .ListItems()
                .WithListItemName(item => item.Name)
                .WithListItemValue(item => item.Value.ToString())
                .WithDrillDownOnly();
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        CollectionSample sample = new();
        PropertyBag bag = DiagnosticManager.ObjectToPropertyBag(sample, "Collection", "Tests");
        Assert.True(bag.GetProperty("One", null).CanDrillDown);
        Assert.True(bag.GetProperty("One", null).DrillDownIconOnly);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection||One" } }
        );

        Assert.Equal("1", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(CollectionItem.Value), null).Value);
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
        Property property = bag.GetProperty("Items", null);
        Assert.Equal("3", property.Value);
        Assert.True(property.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection||Items" } }
        );

        Assert.Equal(2, response.DisplayedCount);
        Assert.Equal(3, response.TotalCount);
        Assert.True(response.IsTruncated);
        Assert.Equal(new[] { "Items[0]", "Items[1]" }, response.Diagnostics.PropertyBags.Select(bag => bag.Name));
    }

    [Fact]
    public void CollectionCountSupportsDrillDownAndExpandedHover()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ShowCount().WithDrillDown().WithExpandedHover();
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new CollectionSample()).GetProperty("Items", null);

        Assert.Equal("3", property.Value);
        Assert.True(property.CanDrillDown);
        Assert.True(property.CanExpandedHover);
        Assert.False(property.CanJsonHover);
    }

    [Fact]
    public void EmptyCollectionDoesNotExposeDrillDown()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ShowCount().WithDrillDown().WithExpandedHover();
        });
        DiagnosticManager.UseConfiguration(configuration);

        CollectionSample sample = new();
        sample.Items.Clear();
        Property property = Render(sample).GetProperty("Items", null);

        Assert.Equal("0", property.Value);
        Assert.False(property.CanDrillDown);
        Assert.False(property.CanExpandedHover);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection||Items" } }
        );

        Assert.NotNull(response.ErrorMessage);
    }

    [Fact]
    public void NullExpandedObjectDoesNotExposeDrillDown()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<NullExpandedSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Details).Expand().WithDrillDownOnly();
        });
        DiagnosticManager.UseConfiguration(configuration);

        NullExpandedSample sample = new();
        PropertyBag bag = Render(sample);
        Category category = Assert.Single(bag.Categories);

        Assert.Equal(nameof(NullExpandedSample.Details), category.Name);
        Assert.False(category.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Null expanded") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Null expanded|Details" } }
        );

        Assert.NotNull(response.ErrorMessage);
    }

    [Fact]
    public void ConcatenatedEnumerableSupportsDrillDown()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false, DrillDownMaxItems = 10 };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Items).ConcatItems(", ").WithDrillDown(maxItems: 2);
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        CollectionSample sample = new();
        PropertyBag bag = Render(sample);
        Property property = bag.GetProperty("Items", null);
        Assert.Equal("One, Two, Three", property.Value);
        Assert.True(property.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection||Items" } }
        );

        Assert.Equal(2, response.DisplayedCount);
        Assert.Equal(3, response.TotalCount);
        Assert.True(response.IsTruncated);
        Assert.Equal(new[] { "Items[0]", "Items[1]" }, response.Diagnostics.PropertyBags.Select(bag => bag.Name));
    }

    [Fact]
    public void NamedDelegateCollectionDefaultsToCountAndSupportsDrillDown()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false, DrillDownMaxItems = 10 };
        configuration.Configure<CollectionSample>(type =>
        {
            type.ExcludeAll();
            type.Property("Item inventory", sample => sample.Items).WithDrillDown(maxItems: 2);
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.ExcludeAll();
            type.Include(item => item.Name);
        });
        DiagnosticManager.UseConfiguration(configuration);

        CollectionSample sample = new();
        PropertyBag bag = Render(sample);
        Property property = bag.GetProperty("Item inventory", null);
        Assert.Equal("3", property.Value);
        Assert.True(property.CanDrillDown);

        DrillDownResponse response = DiagnosticManager.GetDrillDown(
            new[] { new RegisteredObject(sample, "Tests", "Collection") },
            new DrillDownRequest { ObjectPaths = new List<string> { "Tests|Collection||Item inventory" } }
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
                ObjectPaths = new List<string> { "Tests|Root||Child", "DrillDown|ChildSample||Details" },
            }
        );

        Assert.Equal("Deep", Assert.Single(response.Diagnostics.PropertyBags).GetProperty(nameof(GrandChildSample.Value), null).Value);
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
                ObjectPaths = new[] { "Tests|Root||Child" },
                Path = "DrillDown|ChildSample||Editable",
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
            type.Property(sample => sample.Requests).WithLabel("Requests").ShowRate(false).ShowTotal();
            type.Property(sample => sample.Started).ShowDate(false).ShowElapsed();
            type.Property(sample => sample.Details).WithLabel("Details").Expand();
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

        Assert.Equal("4", bag.GetProperty("Total Requests", null).Value);
        Assert.Null(bag.GetProperty("Requests/sec", null));
        Assert.NotNull(bag.GetProperty("Time since Started", null));
        Assert.Null(bag.GetProperty(nameof(StrategySample.Started), null));
        Assert.Equal("Nested", bag.GetProperty(nameof(ChildSample.Name), "Details").Value);
    }

    [Fact]
    public void ExpandedPropertiesCanConfigureTheirInitialExpansionState()
    {
        DiagnosticConfiguration collapsedConfiguration = new() { ApplyAttributes = false };
        collapsedConfiguration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Details).Expand(false);
        });
        collapsedConfiguration.Configure<ChildSample>(type => type.IncludeAll());
        DiagnosticManager.UseConfiguration(collapsedConfiguration);

        Category collapsedCategory = Render(new StrategySample()).Categories.Single(category => category.Name == nameof(StrategySample.Details));
        Assert.False(collapsedCategory.IsExpanded);

        DiagnosticConfiguration expandedConfiguration = new() { ApplyAttributes = false };
        expandedConfiguration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Details).Expand(true);
        });
        expandedConfiguration.Configure<ChildSample>(type => type.IncludeAll());
        DiagnosticManager.UseConfiguration(expandedConfiguration);

        Category expandedCategory = Render(new StrategySample()).Categories.Single(category => category.Name == nameof(StrategySample.Details));
        Assert.True(expandedCategory.IsExpanded);
    }

    [Fact]
    public void ExpandedPropertyCategoriesCanRenderStatuses()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Details)
                .Expand()
                .WithIconSize(StatusIconSize.Large)
                .Status(StatusCode.Running, _ => true, "Details are running");
        });
        configuration.Configure<ChildSample>(type => type.IncludeAll());
        DiagnosticManager.UseConfiguration(configuration);

        Category category = Render(new StrategySample()).Categories.Single(category => category.Name == nameof(StrategySample.Details));

        PropertyStatus status = Assert.Single(category.Statuses);
        Assert.Equal((StatusCode.Running, "Details are running"), (status.Status, status.Text));
        Assert.Equal(StatusIconSize.Large, category.StatusIconSize);
    }

    [Fact]
    public void ExpandedPropertiesCanLimitChildPropertiesToPrimaryCategory()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<StrategySample>(type =>
        {
            type.ExcludeAll();
            type.Property(sample => sample.Details).Expand().WithPrimaryPropertiesOnly();
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.ExcludeAll();
            type.Include(sample => sample.Name);
            type.Property(sample => sample.LoggerName).WithCategory("Metadata");
            type.Property(sample => sample.Details).Expand();
        });
        configuration.Configure<GrandChildSample>(type => type.IncludeAll());
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new StrategySample());

        Assert.Equal("Nested", bag.GetProperty(nameof(ChildSample.Name), "Details").Value);
        Assert.Null(bag.GetProperty(nameof(ChildSample.LoggerName), "Details.Metadata"));
        Assert.Null(bag.Categories.FindByName("Details.Details"));
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

        Assert.NotNull(bag.GetProperty("Time since _privateStarted", null));
        Assert.NotNull(bag.GetProperty("Time since Last updated", null));
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

        Assert.Equal("4", bag.GetProperty("Total _privateRequests", null).Value);
        Assert.Equal("4", bag.GetProperty("Total Background requests", null).Value);
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
            type.Property(sample => sample.Details).WithLabel("Configured details").Expand();
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

        Assert.Equal("One, Two, ... (1 more item)", bag.GetProperty("Attributed items", "Collections").Value);
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
            type.Property(sample => sample.BaseValue).WithLabel("Base configured");
        });
        configuration.Configure<DerivedSample>(type =>
        {
            type.Include(sample => sample.DerivedValue);
            type.Property(sample => sample.BaseValue).WithLabel("Derived override");
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new DerivedSample());

        Assert.Equal("Base", bag.GetProperty("Derived override", null).Value);
        Assert.Equal("Derived", bag.GetProperty(nameof(DerivedSample.DerivedValue), null).Value);
        Assert.Null(bag.GetProperty("Base configured", null));
    }

    [Fact]
    public void ConfiguredBaseStartsASeparateDefaultConfigurationSegment()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ConfigurationBoundarySubBaseSample>(type =>
        {
            type.ExcludeAll();
            type.Include(sample => sample.SubBaseVisible);
        });
        configuration.Configure<ConfigurationBoundaryDerivedSample>(type =>
        {
            type.IncludeAll();
            type.Exclude(sample => sample.DerivedHidden);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ConfigurationBoundaryDerivedSample());

        Assert.NotNull(bag.GetProperty(nameof(ConfigurationBoundarySubBaseSample.SubBaseVisible), null));
        Assert.Null(bag.GetProperty(nameof(ConfigurationBoundarySubBaseSample.SubBaseHidden), null));
        Assert.NotNull(bag.GetProperty(nameof(ConfigurationBoundaryMiddleSample.MiddleValue), null));
        Assert.NotNull(bag.GetProperty(nameof(ConfigurationBoundaryDerivedSample.DerivedVisible), null));
        Assert.Null(bag.GetProperty(nameof(ConfigurationBoundaryDerivedSample.DerivedHidden), null));
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
        Assert.NotNull(Render(new ReplacementSample()).GetProperty(nameof(ReplacementSample.First), null));

        DiagnosticConfiguration second = new();
        second.Configure<ReplacementSample>(type =>
        {
            type.ExcludeAll();
            type.Include(sample => sample.Second);
        });
        DiagnosticManager.UseConfiguration(second);
        PropertyBag bag = Render(new ReplacementSample());

        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), null));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), null));
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
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), null));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), null));
    }

    [Fact]
    public void CallbackOnlyHostingExtensionDefersAssemblyConfigurationDiscovery()
    {
        AssemblyConfigurationSample.WasConfigured = false;
        ServiceCollection services = new();

        services.ConfigureDiagnosticExplorer(diagnostics => diagnostics.ConfigureAssemblies(typeof(AssemblyConfigurationSample).Assembly));

        Assert.False(AssemblyConfigurationSample.WasConfigured);
        _ = Render(new AssemblyConfigurationSample());
        Assert.True(AssemblyConfigurationSample.WasConfigured);
    }

    [Fact]
    public void CallbackOnlyHostingExtensionDoesNotThrowWhenRuntimeConfigurationFails()
    {
        ServiceCollection services = new();

        Exception exception = Record.Exception(() =>
            services.ConfigureDiagnosticExplorer(diagnostics =>
                diagnostics.ConfigureHosting(_ => throw new InvalidOperationException("Configuration failed"))
            )
        );

        Assert.Null(exception);
        Assert.False(DiagnosticManager.Enabled);
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
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), null));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), null));
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
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), null));
    }

    [Fact]
    public void AssemblyConfigurationDiscoversStaticTypeConfigurators()
    {
        AssemblyConfigurationSample.WasConfigured = false;
        DiagnosticConfiguration configuration = new();

        configuration.ConfigureAssemblies(typeof(AssemblyConfigurationSample).Assembly);
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new AssemblyConfigurationSample());

        Assert.True(AssemblyConfigurationSample.WasConfigured);
        Assert.NotNull(bag.GetProperty(nameof(AssemblyConfigurationSample.Visible), null));
        Assert.Null(bag.GetProperty(nameof(AssemblyConfigurationSample.Hidden), null));
    }

    [Fact]
    public void HostingExtensionDefersAssemblyConfigurationDiscovery()
    {
        AssemblyConfigurationSample.WasConfigured = false;
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();

        services.ConfigureDiagnosticExplorer(
            hostConfiguration,
            diagnostics => diagnostics.ConfigureAssemblies(typeof(AssemblyConfigurationSample).Assembly)
        );

        Assert.False(AssemblyConfigurationSample.WasConfigured);
        _ = Render(new AssemblyConfigurationSample());
        Assert.True(AssemblyConfigurationSample.WasConfigured);
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
                    type.Property(sample => sample.First.Length).WithLabel("Invalid nested selector");
                    type.Property(sample => sample.Second);
                })
        );

        PropertyBag bag = Render(new ReplacementSample());

        Assert.True(DiagnosticManager.Enabled);
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First), null));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second), null));
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
        public DefaultPoint Point { get; } = new(3, 4);
        public ChildSample Child { get; } = new();
        public IReadOnlyList<string> Items { get; } = new[] { "One", "Two" };
        public System.Threading.Tasks.Task Pending { get; } = System.Threading.Tasks.Task.CompletedTask;
    }

    private readonly struct DefaultPoint
    {
        public DefaultPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public override string ToString() => $"({X}, {Y})";
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
                .ListItems()
                .WithListItemName(item => item.Name)
                .WithListItemValue(item => item.Value.ToString());
        }

        public static void ConfigurePrivateItemsFromField(ITypeConfigurator<CollectionSample> type)
        {
            type.Property(sample => sample._privateItems)
                .ListItems()
                .WithListItemName(item => item.Name)
                .WithListItemValue(item => item.Value.ToString());
        }
    }

    private sealed class NullExpandedSample
    {
        public ChildSample Details => null;
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

    private sealed class ConcreteCollectionSample
    {
        public List<CollectionItem> List { get; } = new() { new("List", 1) };
        public HashSet<CollectionItem> Set { get; } = new() { new("Set", 1) };
        public ObservableCollection<CollectionItem> Observable { get; } = new() { new("Observable", 1) };
        public BindingList<CollectionItem> Binding { get; } = new() { new("Binding", 1) };
        public Dictionary<string, CollectionItem> Dictionary { get; } = new() { ["First"] = new("Dictionary", 1) };
        public IDictionary<string, CollectionItem> InterfaceDictionary { get; } =
            new Dictionary<string, CollectionItem> { ["Second"] = new("Interface dictionary", 1) };
        public IReadOnlyDictionary<string, CollectionItem> ReadOnlyDictionary { get; } =
            new Dictionary<string, CollectionItem> { ["Third"] = new("Read-only dictionary", 1) };
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

    private class ConfigurationBoundarySubBaseSample
    {
        public string SubBaseVisible { get; } = "Sub-base visible";
        public string SubBaseHidden { get; } = "Sub-base hidden";
    }

    private class ConfigurationBoundaryMiddleSample : ConfigurationBoundarySubBaseSample
    {
        public string MiddleValue { get; } = "Middle value";
    }

    private sealed class ConfigurationBoundaryDerivedSample : ConfigurationBoundaryMiddleSample
    {
        public string DerivedVisible { get; } = "Derived visible";
        public string DerivedHidden { get; } = "Derived hidden";
    }

    private sealed class ReplacementSample
    {
        public string First { get; } = "First";
        public string Second { get; } = "Second";
    }

    private sealed class AssemblyConfigurationSample
    {
        public static bool WasConfigured { get; set; }

        internal static void ConfigureDiagnostics(IDiagConfigurator config)
        {
            WasConfigured = true;
            config.Configure<AssemblyConfigurationSample>(type =>
            {
                type.ExcludeAll();
                type.Include(sample => sample.Visible);
            });
        }

        public string Visible { get; } = "Visible";
        public string Hidden { get; } = "Hidden";
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
