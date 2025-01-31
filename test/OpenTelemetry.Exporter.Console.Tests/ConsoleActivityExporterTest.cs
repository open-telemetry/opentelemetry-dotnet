// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleActivityExporterTest
{
    [Fact]
    public void VerifyConsoleActivityExporterDoesntFailWithoutActivityLinkTags()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);

        var exportedItems = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .AddInMemoryExporter(exportedItems)
            .Build();

        ActivityContext context;
        using (var first = activitySource.StartActivity("first"))
        {
            context = first!.Context;
        }

        exportedItems.Clear();

        var links = new[] { new ActivityLink(context) };
        using (var secondActivity = activitySource.StartActivity(ActivityKind.Internal, links: links, name: "Second"))
        {
        }

        // Assert that an Activity was exported where ActivityLink.Tags == null.
        var activity = exportedItems[0];
        Assert.Equal("Second", activity.DisplayName);
        Assert.Null(activity.Links.First().Tags);

        // Test that the ConsoleExporter correctly handles an Activity without Tags.
        using var consoleExporter = new ConsoleActivityExporter(new ConsoleExporterOptions());
        Assert.Equal(ExportResult.Success, consoleExporter.Export(new Batch<Activity>([activity], 1)));
    }
}
