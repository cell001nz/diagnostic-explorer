using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using MessagePack.Resolvers;
using Xunit;

namespace DiagnosticExplorer.Logging.Tests;

public class MessagePackContractTests
{
    private static readonly MessagePackSerializerOptions SerializerOptions = MessagePackSerializerOptions
        .Standard.WithResolver(ContractlessStandardResolver.Instance)
        .WithSecurity(MessagePackSecurity.UntrustedData);

    [Fact]
    public void RemotePayloadContractsRoundTrip()
    {
        DiagnosticResponse diagnostics = new()
        {
            Context = "Widget",
            PropertyBags =
            {
                new PropertyBag
                {
                    Name = "General",
                    Categories =
                    {
                        new Category("Runtime")
                        {
                            Properties =
                            {
                                new Property("State", "Running")
                                {
                                    Statuses = new List<PropertyStatus> { new(StatusCode.Running, "The widget is running") },
                                },
                            },
                        },
                    },
                },
            },
        };
        DrillDownRequest request = new()
        {
            ObjectPaths = new List<string> { "Widgets", "Current" },
            JsonHover = true,
        };
        DiagnosticMsg[] messages =
        {
            new()
            {
                Date = DateTime.UtcNow,
                Category = "Widgets",
                Message = "Started",
            },
        };

        DiagnosticResponse deserializedDiagnostics = RoundTrip(diagnostics);
        DrillDownRequest deserializedRequest = RoundTrip(request);
        DiagnosticMsg[] deserializedMessages = RoundTrip(messages);

        Assert.Equal("Widget", deserializedDiagnostics.Context);
        Assert.Equal(StatusCode.Running, deserializedDiagnostics.PropertyBags[0].Categories[0].Properties[0].Statuses[0].Status);
        Assert.Equal(request.ObjectPaths, deserializedRequest.ObjectPaths);
        Assert.True(deserializedRequest.JsonHover);
        Assert.Equal("Started", Assert.Single(deserializedMessages).Message);
    }

    [Fact]
    public void EmptyAlertsStatusesAndDefaultIconSizesAreOmittedFromJson()
    {
        Property property = new("State", "Running");
        Category category = new("Runtime");
        JsonSerializerOptions options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        string propertyJson = JsonSerializer.Serialize(property, options);
        string categoryJson = JsonSerializer.Serialize(category, options);

        Assert.DoesNotContain("Alerts", propertyJson);
        Assert.DoesNotContain("Statuses", propertyJson);
        Assert.DoesNotContain("StatusIconSize", propertyJson);
        Assert.DoesNotContain("Statuses", categoryJson);
        Assert.DoesNotContain("StatusIconSize", categoryJson);
    }

    private static T RoundTrip<T>(T value)
    {
        return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value, SerializerOptions), SerializerOptions);
    }
}
