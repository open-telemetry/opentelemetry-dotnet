// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Console.Tests;

[Collection(ConsoleErrorCollectionDefinition.Name)]
public class ConsoleActivityExporterLimitTests
{
    [Fact]
    public void Export_SpanAttributesBeyondDefaultCountLimit_WritesAllowedAttributesAndDropCount()
    {
        using var activity = new Activity("test");
        for (var i = 0; i <= 128; i++)
        {
            activity.SetTag($"key{i}", i);
        }

        var output = Export(activity);

        Assert.Equal(
            Enumerable.Range(0, 128).Select(i => $"    key{i}: {i}").Append("    (1 dropped)"),
            GetSection(output, "Activity.Tags:", "Instrumentation scope (ActivitySource):"));
    }

    [Fact]
    public void Export_EventAttributesBeyondDefaultCountLimit_WritesDropsUnderEachEvent()
    {
        using var activity = new Activity("test");
        var tags = new ActivityTagsCollection();
        for (var i = 0; i <= 128; i++)
        {
            tags[$"key{i}"] = i;
        }

        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        activity.AddEvent(new ActivityEvent("first", timestamp, tags));
        tags = new ActivityTagsCollection(tags) { ["key129"] = 129 };
        activity.AddEvent(new ActivityEvent("second", timestamp, tags));

        var output = Export(activity);

        var expected = new List<string> { $"    first [{timestamp}]" };
        expected.AddRange(Enumerable.Range(0, 128).Select(i => $"        key{i}: {i}"));
        expected.Add("        (1 dropped)");
        expected.Add($"    second [{timestamp}]");
        expected.AddRange(Enumerable.Range(0, 128).Select(i => $"        key{i}: {i}"));
        expected.Add("        (2 dropped)");
        Assert.Equal(expected, GetSection(output, "Activity.Events:", "Instrumentation scope (ActivitySource):"));
    }

    [Theory]
    [InlineData(ActivityStatusCode.Unset, null, "ERROR", "tag description", "StatusCode: Error", "Activity.StatusDescription:  tag description")]
    [InlineData(ActivityStatusCode.Unset, null, "OK", "tag description", "StatusCode: Ok", null)]
    [InlineData(ActivityStatusCode.Unset, null, "UNSET", "tag description", null, null)]
    [InlineData(ActivityStatusCode.Unset, null, "unknown", "tag description", null, null)]
    [InlineData(ActivityStatusCode.Error, "activity description", "OK", "tag description", "StatusCode: Error", "Activity.StatusDescription:  activity description")]
    [InlineData(ActivityStatusCode.Ok, null, "ERROR", "tag description", "StatusCode: Ok", null)]
    public void Export_ActivityAndLegacyStatus_WritesResolvedStatusWithoutStatusAttributes(
        ActivityStatusCode activityStatus,
        string? activityDescription,
        string statusCodeTag,
        string statusDescriptionTag,
        string? expectedStatusLine,
        string? expectedDescriptionLine)
    {
        using var activity = new Activity("test");
        activity.SetStatus(activityStatus, activityDescription);
        activity.SetTag("otel.status_code", statusCodeTag);
        activity.SetTag("otel.status_description", statusDescriptionTag);

        var output = Export(activity);

        // Status tags are never written as attributes.
        Assert.DoesNotContain("otel.status", output, StringComparison.Ordinal);

        if (expectedStatusLine == null)
        {
            Assert.DoesNotContain("StatusCode:", output, StringComparison.Ordinal);
        }
        else
        {
            Assert.Single(GetLines(output), line => line == expectedStatusLine);
        }

        if (expectedDescriptionLine == null)
        {
            Assert.DoesNotContain("StatusDescription", output, StringComparison.Ordinal);
        }
        else
        {
            Assert.Single(GetLines(output), line => line == expectedDescriptionLine);
        }
    }

    [Fact]
    public void Export_DroppedSpanAttributes_WarningIncludesExporterNameAndTotals()
    {
        // Building a provider makes sure the SDK's EventSource exists.
        using (Sdk.CreateTracerProviderBuilder().Build())
        {
        }

        var sdkEventSource = EventSource.GetSources().Single(s => s.Name == "OpenTelemetry-Sdk");
        using var listener = new TestEventListener(sdkEventSource, EventLevel.Warning);

        using var activity = new Activity("test");
        for (var i = 0; i <= 128; i++)
        {
            activity.SetTag($"key{i}", i);
        }

        Export(activity);

        var warning = Assert.Single(listener.Messages, e => e.EventSource.Name == "OpenTelemetry-Sdk" && e.EventId == 60);
        Assert.Equal(["ConsoleActivityExporter", 1L, 1L, 0L, 0L], warning.Payload!);
    }

    [Theory]
    [InlineData("Span")]
    [InlineData("Event")]
    [InlineData("Link")]
    public void Export_RejectedAttributeBeforeCountLimit_FreesCapacityForValidAttributes(string target)
    {
        using var activity = new Activity("test");
        var tags = new ActivityTagsCollection { ["rejected"] = new ThrowingToString() };
        for (var i = 0; i < 129; i++)
        {
            tags.Add($"key{i}", i);
        }

        string section;
        string indent;
        if (target == "Span")
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }

            section = "Activity.Tags:";
            indent = "    ";
        }
        else if (target == "Event")
        {
            activity.AddEvent(new ActivityEvent("event", tags: tags));
            section = "Activity.Events:";
            indent = "        ";
        }
        else
        {
            activity.AddLink(new ActivityLink(default, tags));
            section = "Activity.Links:";
            indent = "        ";
        }

        var lines = GetSection(Export(activity), section, "Instrumentation scope (ActivitySource):");
        if (target != "Span")
        {
            lines = lines.Skip(1).ToArray();
        }

        Assert.Equal(
            Enumerable.Range(0, 128)
                .Select(i => $"{indent}key{i}: {i}")
                .Prepend($"Unsupported attribute value type '{typeof(ThrowingToString).FullName}' for 'rejected'.")
                .Append($"{indent}(2 dropped)"),
            lines);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_EventsOrLinksBeyondDefaultCountLimit_WritesAllowedItemsAndDropCount(bool isLink)
    {
        using var activity = new Activity("test");
        var expected = new List<string>();
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 129; i++)
        {
            if (isLink)
            {
                var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
                activity.AddLink(new ActivityLink(context));
                if (i < 128)
                {
                    expected.Add($"    {context.TraceId} {context.SpanId}");
                }
            }
            else
            {
                activity.AddEvent(new ActivityEvent($"event{i}", timestamp));
                if (i < 128)
                {
                    expected.Add($"    event{i} [{timestamp}]");
                }
            }
        }

        expected.Add("    (1 dropped)");
        Assert.Equal(
            expected,
            GetSection(Export(activity), isLink ? "Activity.Links:" : "Activity.Events:", "Instrumentation scope (ActivitySource):"));
    }

    private static string[] GetLines(string output)
        => output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

    private static string[] GetSection(string output, string header, string nextHeader)
    {
        var lines = GetLines(output);
        var start = Array.IndexOf(lines, header);
        var end = Array.IndexOf(lines, nextHeader);
        Assert.True(start >= 0 && end > start, $"Expected section '{header}' before '{nextHeader}', but got:{Environment.NewLine}{output}");
        return [.. lines.Skip(start + 1).Take(end - start - 1)];
    }

    private static string Export(Activity activity)
    {
        using var exporter = new ConsoleActivityExporter(new ConsoleExporterOptions());

        var originalOut = System.Console.Out;
        using var output = new StringWriter();
        System.Console.SetOut(output);

        try
        {
            Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([activity], 1)));
        }
        finally
        {
            System.Console.SetOut(originalOut);
        }

        return output.ToString();
    }

    private sealed class ThrowingToString
    {
        public override string ToString() => throw new InvalidOperationException("Cannot format this attribute.");
    }
}
