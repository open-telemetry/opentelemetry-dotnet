// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleActivityExporterAdditionalTests
{
    [Fact]
    public void Export_SimpleActivity_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("SimpleActivity"))
        {
            activity?.SetTag("test.key", "test.value");
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithEvents_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("ActivityWithEvents"))
        {
            activity?.AddEvent(new ActivityEvent("Event1", DateTimeOffset.UtcNow, new ActivityTagsCollection { { "event.key", "event.value" } }));
            activity?.AddEvent(new ActivityEvent("Event2"));
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithStatus_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("ActivityWithStatus"))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Something went wrong");
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithTraceState_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("ActivityWithTraceState"))
        {
            activity!.TraceStateString = "key1=value1,key2=value2";
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithParentSpanId_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (var parentActivity = activitySource.StartActivity("ParentActivity"))
        using (var childActivity = activitySource.StartActivity("ChildActivity"))
        {
            // No-op
        }

        // Assert
        Assert.Equal(2, activities.Count);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithMultipleTags_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var exportedItems = new List<Activity>();

        using var activities = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("ActivityWithTags"))
        {
            activity?.SetTag("string.tag", "value");
            activity?.SetTag("int.tag", 42);
            activity?.SetTag("bool.tag", true);
            activity?.SetTag("double.tag", 3.14);
        }

        // Assert
        Assert.Single(exportedItems);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. exportedItems], exportedItems.Count)));
    }

    [Fact]
    public void Export_ActivityWithDifferentKinds_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (activitySource.StartActivity("ClientActivity", ActivityKind.Client))
        using (activitySource.StartActivity("ServerActivity", ActivityKind.Server))
        using (activitySource.StartActivity("ProducerActivity", ActivityKind.Producer))
        using (activitySource.StartActivity("ConsumerActivity", ActivityKind.Consumer))
        using (activitySource.StartActivity("InternalActivity", ActivityKind.Internal))
        {
            // No-op
        }

        // Assert
        Assert.Equal(5, activities.Count);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithSourceVersion_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name, "1.0.0");

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (activitySource.StartActivity("VersionedActivity"))
        {
            // No-op
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithResource_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("TestService", serviceVersion: "1.0.0"))
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (activitySource.StartActivity("ActivityWithResource"))
        {
            // No-op
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_WithDebugTarget()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (activitySource.StartActivity("TestActivity"))
        {
            // No-op
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Debug,
        });
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_WithBothTargets()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (activitySource.StartActivity("TestActivity"))
        {
            // No-op
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug,
        });
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithArrayTags_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("ActivityWithArrayTags"))
        {
#pragma warning disable CA1861 // Avoid constant arrays as arguments
            activity?.SetTag("array.tag", new[] { "value1", "value2", "value3" });
            activity?.SetTag("int.array.tag", new[] { 1, 2, 3 });
#pragma warning restore CA1861 // Avoid constant arrays as arguments
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithLinkWithTags_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        ActivityContext context;
        using (var first = activitySource.StartActivity("first"))
        {
            context = first!.Context;
        }

        activities.Clear();

        var linkTags = new ActivityTagsCollection
        {
            { "link.tag1", "value1" },
            { "link.tag2", 42 },
        };
        var links = new[] { new ActivityLink(context, linkTags) };
        using (activitySource.StartActivity(ActivityKind.Internal, links: links, name: "Second"))
        {
            // No-op
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }

    [Fact]
    public void Export_ActivityWithStatusCodeTag_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddInMemoryExporter(activities)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("ActivityWithStatusTag"))
        {
            activity?.SetTag("otel.status_code", "ERROR");
            activity?.SetTag("otel.status_description", "Test error");
        }

        // Assert
        Assert.Single(activities);

        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([.. activities], activities.Count)));
    }
}
