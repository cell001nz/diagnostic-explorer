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

        Property property = bag.GetProperty(nameof(AttributeSample.Attributed));
        Assert.NotNull(property);
        Assert.Equal("7", property.Value);
        Assert.Null(property.Description);
        Assert.False(property.CanSet);
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Ignored)));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Hidden)));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Plain)));
        Assert.DoesNotContain(bag.Categories, category => category.Name == "Source" || category.Name == "General");
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
                        Assert.Equal("Widgets", destination.SinkCategory);
                        Assert.Equal("Widgets Events", destination.SinkName);
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

        Assert.Equal("2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.Value)).Value);
        Assert.Equal("2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.NullableValue)).Value);

        configuration.DefaultFormat<DateTime>("The date is {0:d MMM yyyy HH:mm:ss}");
        DiagnosticManager.UseConfiguration(configuration);

        bag = Render(new DefaultFormatSample());

        Assert.Equal("The date is 2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.Value)).Value);
        Assert.Equal("The date is 2 Jan 2025 03:04:05", bag.GetProperty(nameof(DefaultFormatSample.NullableValue)).Value);
    }

    [Fact]
    public void FluentPropertyCanUseTypedDelegateFormatter()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.OptIn();
            type.Property(sample => sample.Attributed).Format(value => $"Value: {value:D3}");
        });
        DiagnosticManager.UseConfiguration(configuration);

        Property property = Render(new AttributeSample()).GetProperty("Attributed name", "Source");

        Assert.Equal("Value: 007", property.Value);
    }

    [Fact]
    public void FluentPropertyMetadataCanUseContainingObjectDelegates()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.OptIn();
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
            type.OptIn();
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
            type.OptIn();
            using (type.CreateCategoryScope("Scoped"))
            {
                type.Property(sample => sample.First);
                type.CustomProperty("Computed", sample => sample.First + sample.Second);
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
    public void CustomPropertyUsesDelegateAndFluentMetadata()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.OptIn();
            type.CustomProperty("Computed", sample => sample.First + sample.Second)
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
    public void FluentMetadataOverridesOnlyExplicitAttributeValues()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributeSample>(type =>
        {
            type.OptIn();
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
            type.OptIn();
            type.Exclude(sample => sample.Attributed);
            type.Include(sample => sample.Ignored);
            type.Include(sample => sample.Hidden);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new AttributeSample());

        Assert.Null(bag.GetProperty("Attributed name", "Source"));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Ignored), "General"));
        Assert.NotNull(bag.GetProperty(nameof(AttributeSample.Hidden)));
        Assert.Null(bag.GetProperty(nameof(AttributeSample.Plain)));
    }

    [Fact]
    public void OptOutIncludesPublicPropertiesUnlessExplicitlyExcluded()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<ReplacementSample>(type =>
        {
            type.OptOut();
            type.Exclude(sample => sample.Second);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ReplacementSample());

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First)));
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.Second)));
    }

    [Fact]
    public void FluentPropertyDeclarationsDefaultToGeneralButIncludeDoesNot()
    {
        DiagnosticConfiguration configuration = new() { ApplyAttributes = false };
        configuration.Configure<ReplacementSample>(type =>
        {
            type.OptIn();
            type.Property(sample => sample.First);
            type.Include(sample => sample.Second);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new ReplacementSample());

        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.First), "General"));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second)));
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.Second), "General"));
    }

    [Fact]
    public void CollectionCanExposeCountAndLimitedConcatenatedValues()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.OptIn();
            type.Collection(sample => sample.Items).ShowCount().Concatenate(", ").WithMaxItems(2);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("3", bag.GetProperty("Items count", "General").Value);
        Assert.Equal("3 items: One, Two, ... (1 more item)", bag.GetProperty("Items", "General").Value);
    }

    [Fact]
    public void CollectionCategoriesUseTypedSelectorAndMaxItems()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.OptIn();
            type.Collection(sample => sample.Items).Categories(item => item.Name).WithMaxItems(2);
        });
        configuration.Configure<CollectionItem>(type =>
        {
            type.OptIn();
            type.Include(item => item.Value);
        });
        DiagnosticManager.UseConfiguration(configuration);

        PropertyBag bag = Render(new CollectionSample());

        Assert.Equal("1", bag.GetProperty(nameof(CollectionItem.Value), "General.One").Value);
        Assert.Equal("2", bag.GetProperty(nameof(CollectionItem.Value), "General.Two").Value);
        Assert.DoesNotContain(bag.Categories, category => category.Name == "Three");
    }

    [Fact]
    public void CollectionListUsesTypedSelectorsAndMaxItems()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<CollectionSample>(type =>
        {
            type.OptIn();
            type.Collection(sample => sample.Items).List(list => list.Name(item => item.Name).Value(item => item.Value.ToString())).WithMaxItems(2);
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
            type.OptIn();
            type.Collection(sample => sample.Items)
                .List(list =>
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
    public void FluentStrategiesWorkWithoutSourceAttributesAndApplyToNestedTypes()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<StrategySample>(type =>
        {
            type.OptIn();
            type.Rate(sample => sample.Requests).Named("Requests").ShowRate(false).ShowTotal();
            type.Date(sample => sample.Started).ShowDate(false).ShowElapsed();
            type.Extended(sample => sample.Details).Named("Details");
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.OptIn();
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
    public void FluentStrategiesOverrideSpecializedAttributesAndPreserveTheirMetadata()
    {
        DiagnosticConfiguration configuration = new();
        configuration.Configure<AttributedStrategySample>(type =>
        {
            type.OptIn();
            type.Collection(sample => sample.Items).Concatenate(", ").WithMaxItems(2);
            type.Rate(sample => sample.Requests).ShowRate(false).ShowTotal();
            type.Date(sample => sample.Started).ShowDate(false).ShowElapsed();
            type.Extended(sample => sample.Details).Named("Configured details");
        });
        configuration.Configure<ChildSample>(type =>
        {
            type.OptIn();
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
            type.OptIn();
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
        Assert.Equal("Derived", bag.GetProperty(nameof(DerivedSample.DerivedValue)).Value);
        Assert.Null(bag.GetProperty("Base configured", "General"));
    }

    [Fact]
    public void ReplacingConfigurationInvalidatesGetterCacheWithoutMergingRules()
    {
        DiagnosticConfiguration first = new();
        first.Configure<ReplacementSample>(type =>
        {
            type.OptIn();
            type.Include(sample => sample.First);
        });
        DiagnosticManager.UseConfiguration(first);
        Assert.NotNull(Render(new ReplacementSample()).GetProperty(nameof(ReplacementSample.First)));

        DiagnosticConfiguration second = new();
        second.Configure<ReplacementSample>(type =>
        {
            type.OptIn();
            type.Include(sample => sample.Second);
        });
        DiagnosticManager.UseConfiguration(second);
        PropertyBag bag = Render(new ReplacementSample());

        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First)));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second)));
    }

    [Fact]
    public void HostingExtensionInstallsFluentConfiguration()
    {
        ServiceCollection services = new();
        IConfiguration hostConfiguration = new ConfigurationBuilder().Build();

        services.ConfigureDiagnosticExplorer(diagnostics =>
            diagnostics.Configure<ReplacementSample>(type =>
            {
                type.OptIn();
                type.Include(sample => sample.Second);
            })
        );

        PropertyBag bag = Render(new ReplacementSample());
        Assert.Null(bag.GetProperty(nameof(ReplacementSample.First)));
        Assert.NotNull(bag.GetProperty(nameof(ReplacementSample.Second)));
    }

    [Fact]
    public void InvalidExpressionsAndLimitsAreRejected()
    {
        DiagnosticConfiguration configuration = new();

        Assert.Throws<ArgumentException>(() => configuration.Configure<StrategySample>(type => type.Property(sample => sample.Details.Name)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            configuration.Configure<CollectionSample>(type => type.Collection(sample => sample.Items).WithMaxItems(0))
        );
    }

    private static PropertyBag Render(object value)
    {
        return DiagnosticManager.ObjectToPropertyBag(value, "Test", "Tests");
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

    private sealed class CollectionSample
    {
        public IList<CollectionItem> Items { get; } = new List<CollectionItem> { new("One", 1), new("Two", 2), new("Three", 3) };
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

    private sealed class StrategySample
    {
        public RateCounter Requests { get; } = new(5);
        public DateTime Started { get; } = DateTime.UtcNow.AddMinutes(-1);
        public ChildSample Details { get; } = new();
    }

    private sealed class AttributedStrategySample
    {
        [CollectionCount("Attributed items", "Collections")]
        public IList<CollectionItem> Items { get; } = new List<CollectionItem> { new("One", 1), new("Two", 2), new("Three", 3) };

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
        public string Excluded { get; } = "Excluded";
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
