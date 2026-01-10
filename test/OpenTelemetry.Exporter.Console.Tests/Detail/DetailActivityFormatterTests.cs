// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests.Compact;

public class DetailActivityFormatterTests
{
    [Fact]
    public async Task DetailActivityOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestActivitySource")
            .ConfigureResource(res => res.AddService("console-test"))
            .AddConsoleExporter(configure =>
                {
                    configure.Formatter = "detail";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                })
            .Build();

        // Act
        using var activitySource = new ActivitySource("TestActivitySource");
        var traceId = default(string);
        using (var activity1 = activitySource.StartActivity("TestActivity"))
        {
            traceId = activity1?.TraceId.ToHexString();

            await Task.Delay(100);
        }

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Activity: {0}", output);

        // Check has Trace ID
        Assert.Matches(@"Activity.TraceId:\s+[0-9a-f]{32}", output);
        Assert.NotNull(traceId);
        Assert.Contains(traceId, output, StringComparison.InvariantCulture);

        // Check has Activity name
        Assert.Matches(@"Activity.DisplayName:\s+TestActivity", output);
    }

    [Fact]
    public async Task AdditionalAttributesActivityTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        var resourceAttributes = new Dictionary<string, object>() { { "att1", "val1" } };
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestActivitySource")
            .ConfigureResource(res => res.AddService("console-test").AddAttributes(resourceAttributes))
            .AddConsoleExporter(configure =>
                {
                    configure.Formatter = "detail";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                })
            .Build();

        // Act
        var sourceTags = new ActivityTagsCollection { { "sourceTag1", "zero" } };
        using var activitySource = new ActivitySource("TestActivitySource", "version1", sourceTags);
        var traceId = default(string);
        using (var activity1 = activitySource.StartActivity("TestActivity"))
        {
            traceId = activity1?.TraceId.ToHexString();

            activity1?.AddBaggage("bag1", "alpha");

            var eventTags = new ActivityTagsCollection { { "eventTag1", "beta" } };
            activity1?.AddEvent(new ActivityEvent("event1", tags: eventTags));

            using var otherActivity = new Activity("other1");
            var linkTags = new ActivityTagsCollection { { "linkTag1", "gamma" } };
            activity1?.AddLink(new ActivityLink(otherActivity.Context, linkTags));

            activity1?.AddTag("tag1", 123);

            activity1?.TraceStateString = "state1";

            activity1?.SetStatus(ActivityStatusCode.Ok);

            await Task.Delay(100);
        }

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Activity: {0}", output);

        Assert.Matches(@"att1:\s+val1", output);
        Assert.Matches(@"sourceTag1:\s+zero", output);
        Assert.Matches(@"eventTag1:\s+beta", output);
        Assert.Matches(@"linkTag1:\s+gamma", output);
        Assert.Matches(@"tag1:\s+123", output);
        Assert.Matches(@"Activity.TraceState:\s+state1", output);
        Assert.Matches(@"StatusCode:\s+Ok", output);
    }
}
