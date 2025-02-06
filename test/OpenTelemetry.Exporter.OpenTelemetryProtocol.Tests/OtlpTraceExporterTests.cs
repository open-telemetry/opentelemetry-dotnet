// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Google.Protobuf.Collections;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpTrace = OpenTelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection("xUnitCollectionPreventingTestsThatDependOnSdkConfigurationFromRunningInParallel")]
public sealed class OtlpTraceExporterTests : IDisposable
{
    private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();
    private static readonly ExperimentalOptions DefaultExperimentalOptions = new();

    private readonly ActivityListener activityListener;

    static OtlpTraceExporterTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    public OtlpTraceExporterTests()
    {
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => options.Parent.TraceFlags.HasFlag(ActivityTraceFlags.Recorded)
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(this.activityListener);
    }

    public void Dispose()
    {
        this.activityListener.Dispose();
    }

    [Fact]
    public void AddOtlpTraceExporterNamedOptionsSupported()
    {
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<OtlpExporterOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<OtlpExporterOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddOtlpExporter()
            .AddOtlpExporter("Exporter2", o => { })
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }

    [Fact]
    public void OtlpExporter_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddOtlpExporter());
    }

    [Fact]
    public void UserHttpFactoryCalled()
    {
        OtlpExporterOptions options = new OtlpExporterOptions();

        var defaultFactory = options.HttpClientFactory;

        int invocations = 0;
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.HttpClientFactory = () =>
        {
            invocations++;
            return defaultFactory();
        };

        using (var exporter = new OtlpTraceExporter(options))
        {
            Assert.Equal(1, invocations);
        }

        using (var provider = Sdk.CreateTracerProviderBuilder()
            .AddOtlpExporter(o =>
            {
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
                o.HttpClientFactory = options.HttpClientFactory;
            })
            .Build())
        {
            Assert.Equal(2, invocations);
        }

        options.HttpClientFactory = () => null!;
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var exporter = new OtlpTraceExporter(options);
        });
    }

    [Fact]
    public void ServiceProviderHttpClientFactoryInvoked()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddHttpClient();

        int invocations = 0;

        services.AddHttpClient("OtlpTraceExporter", configureClient: (client) => invocations++);

        services.AddOpenTelemetry().WithTracing(builder => builder
            .AddOtlpExporter(o => o.Protocol = OtlpExportProtocol.HttpProtobuf));

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        Assert.Equal(1, invocations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToOtlpResourceSpansTest(bool includeServiceNameInResource)
    {
        var evenTags = new[] { new KeyValuePair<string, object?>("k0", "v0") };
        var oddTags = new[] { new KeyValuePair<string, object?>("k1", "v1") };
        var sources = new[]
        {
            new ActivitySource("even", "2.4.6"),
            new ActivitySource("odd", "1.3.5"),
        };

        var resourceBuilder = ResourceBuilder.CreateEmpty();
        if (includeServiceNameInResource)
        {
            resourceBuilder.AddService("service-name", "ns1");
        }

        var exportedItems = new List<Activity>();
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(sources[0].Name)
            .AddSource(sources[1].Name)
            .AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems)));

        using var openTelemetrySdk = builder.Build();

        const int numOfSpans = 10;
        bool isEven;
        for (var i = 0; i < numOfSpans; i++)
        {
            isEven = i % 2 == 0;
            var source = sources[i % 2];
            var activityKind = isEven ? ActivityKind.Client : ActivityKind.Server;
            var activityTags = isEven ? evenTags : oddTags;

            using Activity? activity = source.StartActivity($"span-{i}", activityKind, parentContext: default, activityTags);
        }

        Assert.Equal(10, exportedItems.Count);
        var batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
        RunTest(DefaultSdkLimitOptions, batch);

        void RunTest(SdkLimitOptions sdkOptions, Batch<Activity> batch)
        {
            var request = CreateTraceExportRequest(sdkOptions, batch, resourceBuilder.Build());

            Assert.Single(request.ResourceSpans);
            var otlpResource = request.ResourceSpans.First().Resource;
            if (includeServiceNameInResource)
            {
                Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
                Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
            }
            else
            {
                Assert.DoesNotContain(otlpResource.Attributes, kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName);
            }

            var scopeSpans = request.ResourceSpans.First().ScopeSpans;
            Assert.Equal(2, scopeSpans.Count);
            foreach (var scope in scopeSpans)
            {
                Assert.Equal(numOfSpans / 2, scope.Spans.Count);
                Assert.NotNull(scope.Scope);

                var expectedSpanNames = new List<string>();
                var start = scope.Scope.Name == "even" ? 0 : 1;
                for (var i = start; i < numOfSpans; i += 2)
                {
                    expectedSpanNames.Add($"span-{i}");
                }

                var otlpSpans = scope.Spans;
                Assert.Equal(expectedSpanNames.Count, otlpSpans.Count);

                var kv0 = new OtlpCommon.KeyValue { Key = "k0", Value = new OtlpCommon.AnyValue { StringValue = "v0" } };
                var kv1 = new OtlpCommon.KeyValue { Key = "k1", Value = new OtlpCommon.AnyValue { StringValue = "v1" } };

                var expectedTag = scope.Scope.Name == "even"
                    ? kv0
                    : kv1;

                foreach (var otlpSpan in otlpSpans)
                {
                    Assert.Contains(otlpSpan.Name, expectedSpanNames);
                    Assert.Contains(expectedTag, otlpSpan.Attributes);
                }
            }
        }
    }

    [Fact]
    public void ScopeAttributesRemainConsistentAcrossMultipleBatches()
    {
        var activitySourceTags = new TagList
        {
            new("k0", "v0"),
        };

        using var activitySourceWithTags = new ActivitySource($"{nameof(this.ScopeAttributesRemainConsistentAcrossMultipleBatches)}_WithTags", "1.1.1.3", activitySourceTags);
        using var activitySourceWithoutTags = new ActivitySource($"{nameof(this.ScopeAttributesRemainConsistentAcrossMultipleBatches)}_WithoutTags", "1.1.1.4");

        var resourceBuilder = ResourceBuilder.CreateDefault();

        var exportedItems = new List<Activity>();
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(activitySourceWithTags.Name)
            .AddSource(activitySourceWithoutTags.Name)
            .AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems)));

        using var openTelemetrySdk = builder.Build();

        var parentActivity = activitySourceWithTags.StartActivity("parent", ActivityKind.Server, default(ActivityContext));
        var nestedChildActivity = activitySourceWithTags.StartActivity("nested-child", ActivityKind.Client);
        parentActivity?.Dispose();
        nestedChildActivity?.Dispose();

        Assert.Equal(2, exportedItems.Count);
        var batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
        RunTest(DefaultSdkLimitOptions, batch, activitySourceWithTags);

        exportedItems.Clear();

        var parentActivityNoTags = activitySourceWithoutTags.StartActivity("parent", ActivityKind.Server, default(ActivityContext));
        parentActivityNoTags?.Dispose();

        Assert.Single(exportedItems);
        batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
        RunTest(DefaultSdkLimitOptions, batch, activitySourceWithoutTags);

        void RunTest(SdkLimitOptions sdkOptions, Batch<Activity> batch, ActivitySource activitySource)
        {
            var request = CreateTraceExportRequest(sdkOptions, batch, resourceBuilder.Build());

            var resourceSpans = request.ResourceSpans.First();
            Assert.NotNull(request.ResourceSpans.First());

            var scopeSpans = resourceSpans.ScopeSpans.First();
            Assert.NotNull(scopeSpans);

            var scope = scopeSpans.Scope;
            Assert.NotNull(scope);

            Assert.Equal(activitySource.Name, scope.Name);
            Assert.Equal(activitySource.Version, scope.Version);
            Assert.Equal(activitySource.Tags?.Count() ?? 0, scope.Attributes.Count);

            foreach (var tag in activitySource.Tags ?? [])
            {
                Assert.Contains(scope.Attributes, (kvp) => kvp.Key == tag.Key && kvp.Value.StringValue == (string?)tag.Value);
            }

            // Return and re-add batch to simulate reuse
            request = CreateTraceExportRequest(DefaultSdkLimitOptions, batch, ResourceBuilder.CreateDefault().Build());

            resourceSpans = request.ResourceSpans.First();
            scopeSpans = resourceSpans.ScopeSpans.First();
            scope = scopeSpans.Scope;

            Assert.Equal(activitySource.Name, scope.Name);
            Assert.Equal(activitySource.Version, scope.Version);
            Assert.Equal(activitySource.Tags?.Count() ?? 0, scope.Attributes.Count);

            foreach (var tag in activitySource.Tags ?? [])
            {
                Assert.Contains(scope.Attributes, (kvp) => kvp.Key == tag.Key && kvp.Value.StringValue == (string?)tag.Value);
            }
        }
    }

    [Fact]
    public void ScopeAttributesLimitsTest()
    {
        var sdkOptions = new SdkLimitOptions()
        {
            AttributeValueLengthLimit = 4,
            AttributeCountLimit = 3,
        };

        // ActivitySource Tags are sorted in .NET.
        var activitySourceTags = new TagList
        {
            new("1_TruncatedSourceTag", "12345"),
            new("2_TruncatedSourceStringArray", new string?[] { "12345", "1234", string.Empty, null }),
            new("3_TruncatedSourceObjectTag", new object()),
            new("4_OneSourceTagTooMany", 1),
        };

        var resourceBuilder = ResourceBuilder.CreateDefault();

        using var activitySource = new ActivitySource(name: nameof(this.ScopeAttributesLimitsTest), tags: activitySourceTags);

        var exportedItems = new List<Activity>();
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(activitySource.Name)
            .AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems)));

        using var openTelemetrySdk = builder.Build();

        var activity = activitySource.StartActivity("parent", ActivityKind.Server, default(ActivityContext));
        activity?.Dispose();

        Assert.Single(exportedItems);
        var batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
        RunTest(sdkOptions, batch);

        void RunTest(SdkLimitOptions sdkOptions, Batch<Activity> batch)
        {
            var request = CreateTraceExportRequest(sdkOptions, batch, resourceBuilder.Build());

            var resourceSpans = request.ResourceSpans.First();
            Assert.NotNull(request.ResourceSpans.First());

            var scopeSpans = resourceSpans.ScopeSpans.First();
            Assert.NotNull(scopeSpans);

            var scope = scopeSpans.Scope;
            Assert.NotNull(scope);

            Assert.Equal(3, scope.Attributes.Count);
            Assert.Equal(1u, scope.DroppedAttributesCount);
            Assert.Equal("1234", scope.Attributes[0].Value.StringValue);
            this.ArrayValueAsserts(scope.Attributes[1].Value.ArrayValue.Values);
            Assert.Equal(new object().ToString()!.Substring(0, 4), scope.Attributes[2].Value.StringValue);
        }
    }

    [Fact]
    public void SpanLimitsTest()
    {
        var sdkOptions = new SdkLimitOptions()
        {
            AttributeValueLengthLimit = 4,
            AttributeCountLimit = 3,
            SpanEventCountLimit = 1,
            SpanLinkCountLimit = 1,
        };

        var tags = new ActivityTagsCollection
        {
            new("TruncatedTag", "12345"),
            new("TruncatedStringArray", new string?[] { "12345", "1234", string.Empty, null }),
            new("TruncatedObjectTag", new object()),
            new("OneTagTooMany", 1),
        };

        var links = new[]
        {
            new ActivityLink(default, tags),
            new ActivityLink(default, tags),
        };

        using var activitySource = new ActivitySource(nameof(this.SpanLimitsTest));
        using var activity = activitySource.StartActivity("root", ActivityKind.Server, default(ActivityContext), tags, links);

        Assert.NotNull(activity);

        var event1 = new ActivityEvent("Event", DateTime.UtcNow, tags);
        var event2 = new ActivityEvent("OneEventTooMany", DateTime.Now, tags);

        activity.AddEvent(event1);
        activity.AddEvent(event2);

        var otlpSpan = ToOtlpSpan(sdkOptions, activity);

        Assert.NotNull(otlpSpan);
        Assert.Equal(3, otlpSpan.Attributes.Count);
        Assert.Equal(1u, otlpSpan.DroppedAttributesCount);
        Assert.Equal("1234", otlpSpan.Attributes[0].Value.StringValue);
        this.ArrayValueAsserts(otlpSpan.Attributes[1].Value.ArrayValue.Values);
        Assert.Equal(new object().ToString()!.Substring(0, 4), otlpSpan.Attributes[2].Value.StringValue);

        Assert.Single(otlpSpan.Events);
        Assert.Equal(1u, otlpSpan.DroppedEventsCount);
        Assert.Equal(3, otlpSpan.Events[0].Attributes.Count);
        Assert.Equal(1u, otlpSpan.Events[0].DroppedAttributesCount);
        Assert.Equal("1234", otlpSpan.Events[0].Attributes[0].Value.StringValue);
        this.ArrayValueAsserts(otlpSpan.Events[0].Attributes[1].Value.ArrayValue.Values);
        Assert.Equal(new object().ToString()!.Substring(0, 4), otlpSpan.Events[0].Attributes[2].Value.StringValue);

        Assert.Single(otlpSpan.Links);
        Assert.Equal(1u, otlpSpan.DroppedLinksCount);
        Assert.Equal(3, otlpSpan.Links[0].Attributes.Count);
        Assert.Equal(1u, otlpSpan.Links[0].DroppedAttributesCount);
        Assert.Equal("1234", otlpSpan.Links[0].Attributes[0].Value.StringValue);
        this.ArrayValueAsserts(otlpSpan.Links[0].Attributes[1].Value.ArrayValue.Values);
        Assert.Equal(new object().ToString()!.Substring(0, 4), otlpSpan.Links[0].Attributes[2].Value.StringValue);
    }

    [Fact]
    public void ToOtlpSpanTest()
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

        using var rootActivity = activitySource.StartActivity("root", ActivityKind.Producer);

        var attributes = new List<KeyValuePair<string, object?>>
        {
            new("bool", true),
            new("long", 1L),
            new("string", "text"),
            new("double", 3.14),
            new("int", 1),
            new("datetime", DateTime.UtcNow),
            new("bool_array", new bool[] { true, false }),
            new("int_array", new int[] { 1, 2 }),
            new("double_array", new double[] { 1.0, 2.09 }),
            new("string_array", new string[] { "a", "b" }),
            new("datetime_array", new DateTime[] { DateTime.UtcNow, DateTime.Now }),
        };

        Assert.NotNull(rootActivity);
        foreach (var kvp in attributes)
        {
            rootActivity.SetTag(kvp.Key, kvp.Value);
        }

        var startTime = new DateTime(2020, 02, 20, 20, 20, 20, DateTimeKind.Utc);

        DateTimeOffset dateTimeOffset;
        dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(0);

        var expectedUnixTimeTicks = (ulong)(startTime.Ticks - dateTimeOffset.Ticks);
        var duration = TimeSpan.FromMilliseconds(1555);

        rootActivity.SetStartTime(startTime);
        rootActivity.SetEndTime(startTime + duration);

        Span<byte> traceIdSpan = stackalloc byte[16];
        rootActivity.TraceId.CopyTo(traceIdSpan);
        var traceId = traceIdSpan.ToArray();

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, rootActivity);

        Assert.NotNull(otlpSpan);
        Assert.Equal("root", otlpSpan.Name);
        Assert.Equal(OtlpTrace.Span.Types.SpanKind.Producer, otlpSpan.Kind);
        Assert.Equal(traceId, otlpSpan.TraceId);
        Assert.Empty(otlpSpan.ParentSpanId);
        Assert.Null(otlpSpan.Status);
        Assert.Empty(otlpSpan.Events);
        Assert.Empty(otlpSpan.Links);
        OtlpTestHelpers.AssertOtlpAttributes(attributes, otlpSpan.Attributes);

        var expectedStartTimeUnixNano = 100 * expectedUnixTimeTicks;
        Assert.Equal(expectedStartTimeUnixNano, otlpSpan.StartTimeUnixNano);
        var expectedEndTimeUnixNano = expectedStartTimeUnixNano + (duration.TotalMilliseconds * 1_000_000);
        Assert.Equal(expectedEndTimeUnixNano, otlpSpan.EndTimeUnixNano);

        var childLinks = new List<ActivityLink> { new(rootActivity.Context, new ActivityTagsCollection(attributes)) };
        var childActivity = activitySource.StartActivity(
            "child",
            ActivityKind.Client,
            rootActivity.Context,
            links: childLinks);

        Assert.NotNull(childActivity);

        childActivity.SetStatus(ActivityStatusCode.Error, new string('a', 150));

        var childEvents = new List<ActivityEvent> { new("e0"), new("e1", default, new ActivityTagsCollection(attributes)) };
        childActivity.AddEvent(childEvents[0]);
        childActivity.AddEvent(childEvents[1]);

        Span<byte> parentIdSpan = stackalloc byte[8];
        rootActivity.Context.SpanId.CopyTo(parentIdSpan);
        var parentId = parentIdSpan.ToArray();

        otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, childActivity);

        Assert.NotNull(otlpSpan);
        Assert.Equal("child", otlpSpan.Name);
        Assert.Equal(OtlpTrace.Span.Types.SpanKind.Client, otlpSpan.Kind);
        Assert.Equal(traceId, otlpSpan.TraceId);
        Assert.Equal(parentId, otlpSpan.ParentSpanId);

        Assert.NotNull(otlpSpan.Status);
        Assert.Equal(OtlpTrace.Status.Types.StatusCode.Error, otlpSpan.Status.Code);

        Assert.Equal(childActivity.StatusDescription ?? string.Empty, otlpSpan.Status.Message);
        Assert.Empty(otlpSpan.Attributes);

        Assert.Equal(childEvents.Count, otlpSpan.Events.Count);
        for (var i = 0; i < childEvents.Count; i++)
        {
            Assert.Equal(childEvents[i].Name, otlpSpan.Events[i].Name);
            OtlpTestHelpers.AssertOtlpAttributes(childEvents[i].Tags.ToList(), otlpSpan.Events[i].Attributes);
        }

        childLinks.Reverse();
        Assert.Equal(childLinks.Count, otlpSpan.Links.Count);
        for (var i = 0; i < childLinks.Count; i++)
        {
            var tags = childLinks[i].Tags;
            Assert.NotNull(tags);
            OtlpTestHelpers.AssertOtlpAttributes(tags, otlpSpan.Links[i].Attributes);
        }

        var flags = (OtlpTrace.SpanFlags)otlpSpan.Flags;
        Assert.True(flags.HasFlag(OtlpTrace.SpanFlags.ContextHasIsRemoteMask));
        Assert.False(flags.HasFlag(OtlpTrace.SpanFlags.ContextIsRemoteMask));
    }

    [Fact]
    public void ToOtlpSpanActivitiesWithNullArrayTest()
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

        using var rootActivity = activitySource.StartActivity("root", ActivityKind.Client);
        Assert.NotNull(rootActivity);

        var stringArr = new string?[] { "test", string.Empty, null };
        rootActivity.SetTag("stringArray", stringArr);

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, rootActivity);

        Assert.NotNull(otlpSpan);

        var stringArray = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == "stringArray");

        Assert.NotNull(stringArray);
        Assert.Equal("test", stringArray.Value.ArrayValue.Values[0].StringValue);
        Assert.Equal(string.Empty, stringArray.Value.ArrayValue.Values[1].StringValue);
        Assert.Equal(OtlpCommon.AnyValue.ValueOneofCase.None, stringArray.Value.ArrayValue.Values[2].ValueCase);
    }

    [Theory]
    [InlineData(ActivityStatusCode.Unset, "Description will be ignored if status is Unset.")]
    [InlineData(ActivityStatusCode.Ok, "Description will be ignored if status is Okay.")]
    [InlineData(ActivityStatusCode.Error, "Description will be kept if status is Error.")]
    [InlineData(ActivityStatusCode.Error, "150 Character String - aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ToOtlpSpanNativeActivityStatusTest(ActivityStatusCode expectedStatusCode, string statusDescription)
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));
        using var activity = activitySource.StartActivity("Name");
        Assert.NotNull(activity);
        activity.SetStatus(expectedStatusCode, statusDescription);

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, activity);
        Assert.NotNull(otlpSpan);
        if (expectedStatusCode == ActivityStatusCode.Unset)
        {
            Assert.Null(otlpSpan.Status);
        }
        else
        {
            Assert.NotNull(otlpSpan.Status);
            Assert.Equal((int)expectedStatusCode, (int)otlpSpan.Status.Code);
            if (expectedStatusCode == ActivityStatusCode.Error)
            {
                Assert.Equal(statusDescription, otlpSpan.Status.Message);
            }

            if (expectedStatusCode == ActivityStatusCode.Ok)
            {
                Assert.Empty(otlpSpan.Status.Message);
            }
        }
    }

    [Fact]
    public void TracesSerialization_ExpandsBufferForTracesAndSerializes()
    {
        var tags = new ActivityTagsCollection
        {
            new("Tagkey", "Tagvalue"),
        };

        using var activitySource = new ActivitySource(nameof(this.TracesSerialization_ExpandsBufferForTracesAndSerializes));
        using var activity = activitySource.StartActivity("root", ActivityKind.Server, default(ActivityContext), tags);

        Assert.NotNull(activity);
        var batch = new Batch<Activity>([activity], 1);
        RunTest(new(), batch);

        void RunTest(SdkLimitOptions sdkOptions, Batch<Activity> batch)
        {
            var buffer = new byte[50];
            var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, sdkOptions, ResourceBuilder.CreateEmpty().Build(), batch);
            using var stream = new MemoryStream(buffer, 0, writePosition);
            var tracesData = OtlpTrace.TracesData.Parser.ParseFrom(stream);
            var request = new OtlpCollector.ExportTraceServiceRequest();
            request.ResourceSpans.Add(tracesData.ResourceSpans);

            // Buffer should be expanded to accommodate the large array.
            Assert.True(buffer.Length > 50);

            Assert.Single(request.ResourceSpans);
            var scopeSpans = request.ResourceSpans.First().ScopeSpans;
            Assert.Single(scopeSpans);
            var otlpSpan = scopeSpans.First().Spans.First();
            Assert.NotNull(otlpSpan);

            // The string is too large, hence not evaluating the content.
            var keyValue = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == "Tagkey");
            Assert.NotNull(keyValue);
            Assert.Equal("Tagvalue", keyValue.Value.StringValue);
        }
    }

    [Theory]
    [InlineData(StatusCode.Unset, "Unset", "Description will be ignored if status is Unset.")]
    [InlineData(StatusCode.Ok, "Ok", "Description must only be used with the Error StatusCode.")]
    [InlineData(StatusCode.Error, "Error", "Error description.")]
    [InlineData(StatusCode.Error, "Error", "150 Character String - aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ToOtlpSpanStatusTagTest(StatusCode expectedStatusCode, string statusCodeTagValue, string statusDescription)
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));
        using var activity = activitySource.StartActivity("Name");
        Assert.NotNull(activity);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, statusCodeTagValue);
        activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, statusDescription);

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, activity);

        Assert.NotNull(otlpSpan);
        Assert.NotNull(otlpSpan.Status);
        Assert.Equal((int)expectedStatusCode, (int)otlpSpan.Status.Code);

        if (expectedStatusCode == StatusCode.Error)
        {
            Assert.Equal(statusDescription, otlpSpan.Status.Message);
        }
        else
        {
            Assert.Empty(otlpSpan.Status.Message);
        }
    }

    [Theory]
    [InlineData(StatusCode.Unset, "uNsET")]
    [InlineData(StatusCode.Ok, "oK")]
    [InlineData(StatusCode.Error, "ERROR")]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ToOtlpSpanStatusTagIsCaseInsensitiveTest(StatusCode expectedStatusCode, string statusCodeTagValue)
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));
        using var activity = activitySource.StartActivity("Name");
        Assert.NotNull(activity);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, statusCodeTagValue);

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, activity);

        Assert.NotNull(otlpSpan);
        Assert.NotNull(otlpSpan.Status);
        Assert.Equal((int)expectedStatusCode, (int)otlpSpan.Status.Code);
    }

    [Fact]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ToOtlpSpanActivityStatusTakesPrecedenceOverStatusTagsWhenActivityStatusCodeIsOk()
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));
        using var activity = activitySource.StartActivity("Name");
        const string tagDescriptionOnError = "Description when TagStatusCode is Error.";
        Assert.NotNull(activity);
        activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "ERROR");
        activity.SetTag(SpanAttributeConstants.StatusDescriptionKey, tagDescriptionOnError);

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, activity);

        Assert.NotNull(otlpSpan);
        Assert.NotNull(otlpSpan.Status);
        Assert.Equal((int)ActivityStatusCode.Ok, (int)otlpSpan.Status.Code);
        Assert.Empty(otlpSpan.Status.Message);
    }

    [Fact]
    [Obsolete("Remove when ActivityExtensions status APIs are removed")]
    public void ToOtlpSpanActivityStatusTakesPrecedenceOverStatusTagsWhenActivityStatusCodeIsError()
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));
        using var activity = activitySource.StartActivity("Name");
        const string statusDescriptionOnError = "Description when ActivityStatusCode is Error.";
        Assert.NotNull(activity);
        activity.SetStatus(ActivityStatusCode.Error, statusDescriptionOnError);
        activity.SetTag(SpanAttributeConstants.StatusCodeKey, "OK");

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, activity);

        Assert.NotNull(otlpSpan);
        Assert.NotNull(otlpSpan.Status);
        Assert.Equal((int)ActivityStatusCode.Error, (int)otlpSpan.Status.Code);
        Assert.Equal(statusDescriptionOnError, otlpSpan.Status.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToOtlpSpanTraceStateTest(bool traceStateWasSet)
    {
        using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));
        using var activity = activitySource.StartActivity("Name");
        Assert.NotNull(activity);
        string tracestate = "a=b;c=d";
        if (traceStateWasSet)
        {
            activity.TraceStateString = tracestate;
        }

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, activity);
        Assert.NotNull(otlpSpan);

        if (traceStateWasSet)
        {
            Assert.NotNull(otlpSpan.TraceState);
            Assert.Equal(tracestate, otlpSpan.TraceState);
        }
        else
        {
            Assert.Equal(string.Empty, otlpSpan.TraceState);
        }
    }

    [Fact]
    public void UseOpenTelemetryProtocolActivityExporterWithCustomActivityProcessor()
    {
        const string ActivitySourceName = "otlp.test";
        TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

        bool startCalled = false;
        bool endCalled = false;

        testActivityProcessor.StartAction =
            (a) =>
            {
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                endCalled = true;
            };

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddSource(ActivitySourceName)
                        .AddProcessor(testActivityProcessor)
                        .AddOtlpExporter()
                        .Build();

        using var source = new ActivitySource(ActivitySourceName);
        var activity = source.StartActivity("Test Otlp Activity");
        activity?.Stop();

        Assert.True(startCalled);
        Assert.True(endCalled);
    }

    [Fact]
    public void Shutdown_ClientShutdownIsCalled()
    {
        var exportClientMock = new TestExportClient();

        var exporterOptions = new OtlpExporterOptions();
        var transmissionHandler = new OtlpExporterTransmissionHandler(exportClientMock, exporterOptions.TimeoutMilliseconds);

        using var exporter = new OtlpTraceExporter(new OtlpExporterOptions(), DefaultSdkLimitOptions, DefaultExperimentalOptions, transmissionHandler);
        exporter.Shutdown();

        Assert.True(exportClientMock.ShutdownCalled);
    }

    [Fact]
    public void Null_BatchExportProcessorOptions_SupportedTest()
    {
        Sdk.CreateTracerProviderBuilder()
            .AddOtlpExporter(
                o =>
                {
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.ExportProcessorType = ExportProcessorType.Batch;
                    o.BatchExportProcessorOptions = null!;
                });
    }

    [Fact]
    public void NonnamedOptionsMutateSharedInstanceTest()
    {
        var testOptionsInstance = new OtlpExporterOptions();

        OtlpExporterOptions? tracerOptions = null;
        OtlpExporterOptions? meterOptions = null;

        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithTracing(builder => builder.AddOtlpExporter(o =>
            {
                Assert.Equal(testOptionsInstance.Endpoint, o.Endpoint);

                tracerOptions = o;
                o.Endpoint = new("http://localhost/traces");
            }))
            .WithMetrics(builder => builder.AddOtlpExporter(o =>
            {
                Assert.Equal(testOptionsInstance.Endpoint, o.Endpoint);

                meterOptions = o;
                o.Endpoint = new("http://localhost/metrics");
            }));

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        // Verify the OtlpTraceExporter saw the correct endpoint.

        Assert.NotNull(tracerOptions);
        Assert.Null(meterOptions);
        Assert.Equal("http://localhost/traces", tracerOptions.Endpoint.OriginalString);

        var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

        // Verify the OtlpMetricExporter saw the correct endpoint.

        Assert.NotNull(tracerOptions);
        Assert.NotNull(meterOptions);
        Assert.Equal("http://localhost/metrics", meterOptions.Endpoint.OriginalString);

        Assert.False(ReferenceEquals(tracerOptions, meterOptions));
    }

    [Fact]
    public void NamedOptionsMutateSeparateInstancesTest()
    {
        OtlpExporterOptions? tracerOptions = null;
        OtlpExporterOptions? meterOptions = null;

        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithTracing(builder => builder.AddOtlpExporter("Trace", o =>
            {
                tracerOptions = o;
                o.Endpoint = new("http://localhost/traces");
            }))
            .WithMetrics(builder => builder.AddOtlpExporter("Metrics", o =>
            {
                meterOptions = o;
                o.Endpoint = new("http://localhost/metrics");
            }));

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        // Verify the OtlpTraceExporter saw the correct endpoint.

        Assert.NotNull(tracerOptions);
        Assert.Null(meterOptions);
        Assert.Equal("http://localhost/traces", tracerOptions.Endpoint.OriginalString);

        var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

        // Verify the OtlpMetricExporter saw the correct endpoint.

        Assert.NotNull(tracerOptions);
        Assert.NotNull(meterOptions);
        Assert.Equal("http://localhost/metrics", meterOptions.Endpoint.OriginalString);

        // Verify expected state of instances.

        Assert.False(ReferenceEquals(tracerOptions, meterOptions));
        Assert.Equal("http://localhost/traces", tracerOptions.Endpoint.OriginalString);
        Assert.Equal("http://localhost/metrics", meterOptions.Endpoint.OriginalString);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SpanFlagsTest(bool isRecorded, bool isRemote)
    {
        using var activitySource = new ActivitySource(nameof(this.SpanFlagsTest));

        ActivityContext ctx = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            isRecorded ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None,
            isRemote: isRemote);

        using var rootActivity = activitySource.StartActivity("root", ActivityKind.Server, ctx);
        Assert.NotNull(rootActivity);

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, rootActivity);

        Assert.NotNull(otlpSpan);
        var flags = (OtlpTrace.SpanFlags)otlpSpan.Flags;

        ActivityTraceFlags traceFlags = (ActivityTraceFlags)(flags & OtlpTrace.SpanFlags.TraceFlagsMask);

        if (isRecorded)
        {
            Assert.True(traceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }
        else
        {
            Assert.False(traceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }

        Assert.True(flags.HasFlag(OtlpTrace.SpanFlags.ContextHasIsRemoteMask));

        if (isRemote)
        {
            Assert.True(flags.HasFlag(OtlpTrace.SpanFlags.ContextIsRemoteMask));
        }
        else
        {
            Assert.False(flags.HasFlag(OtlpTrace.SpanFlags.ContextIsRemoteMask));
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SpanLinkFlagsTest(bool isRecorded, bool isRemote)
    {
        using var activitySource = new ActivitySource(nameof(this.SpanLinkFlagsTest));

        ActivityContext ctx = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            isRecorded ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None,
            isRemote: isRemote);

        var links = new[]
        {
            new ActivityLink(ctx),
        };

        using var rootActivity = activitySource.StartActivity("root", ActivityKind.Server, default(ActivityContext), links: links);
        Assert.NotNull(rootActivity);

        var otlpSpan = ToOtlpSpan(DefaultSdkLimitOptions, rootActivity);

        Assert.NotNull(otlpSpan);
        var spanLink = Assert.Single(otlpSpan.Links);

        var flags = (OtlpTrace.SpanFlags)spanLink.Flags;

        ActivityTraceFlags traceFlags = (ActivityTraceFlags)(flags & OtlpTrace.SpanFlags.TraceFlagsMask);

        if (isRecorded)
        {
            Assert.True(traceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }
        else
        {
            Assert.False(traceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }

        Assert.True(flags.HasFlag(OtlpTrace.SpanFlags.ContextHasIsRemoteMask));

        if (isRemote)
        {
            Assert.True(flags.HasFlag(OtlpTrace.SpanFlags.ContextIsRemoteMask));
        }
        else
        {
            Assert.False(flags.HasFlag(OtlpTrace.SpanFlags.ContextIsRemoteMask));
        }
    }

    private static OtlpTrace.Span? ToOtlpSpan(SdkLimitOptions sdkOptions, Activity activity)
    {
        var buffer = new byte[4096];
        var writePosition = ProtobufOtlpTraceSerializer.WriteSpan(buffer, 0, sdkOptions, activity);
        using var stream = new MemoryStream(buffer, 0, writePosition);
        var scopeSpans = OtlpTrace.ScopeSpans.Parser.ParseFrom(stream);
        return scopeSpans.Spans.FirstOrDefault();
    }

    private static OtlpCollector.ExportTraceServiceRequest CreateTraceExportRequest(SdkLimitOptions sdkOptions, in Batch<Activity> batch, Resource resource)
    {
        var buffer = new byte[4096];
        var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, sdkOptions, resource, batch);
        using var stream = new MemoryStream(buffer, 0, writePosition);
        var tracesData = OtlpTrace.TracesData.Parser.ParseFrom(stream);
        var request = new OtlpCollector.ExportTraceServiceRequest();
        request.ResourceSpans.Add(tracesData.ResourceSpans);
        return request;
    }

    private void ArrayValueAsserts(RepeatedField<OtlpCommon.AnyValue> values)
    {
        var expectedStringArray = new string?[] { "1234", "1234", string.Empty, null };
        for (var i = 0; i < expectedStringArray.Length; ++i)
        {
            var expectedValue = expectedStringArray[i];
            var expectedValueCase = expectedValue != null
                ? OtlpCommon.AnyValue.ValueOneofCase.StringValue
                : OtlpCommon.AnyValue.ValueOneofCase.None;

            var actual = values[i];
            Assert.Equal(expectedValueCase, actual.ValueCase);
            if (expectedValueCase != OtlpCommon.AnyValue.ValueOneofCase.None)
            {
                Assert.Equal(expectedValue, actual.StringValue);
            }
        }
    }
}
