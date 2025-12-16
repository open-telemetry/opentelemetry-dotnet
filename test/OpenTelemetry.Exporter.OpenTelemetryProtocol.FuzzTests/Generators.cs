// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using FsCheck;
using FsCheck.Fluent;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

internal static class Generators
{
    public static readonly ActivitySource TestActivitySource = new("Fuzz.ActivitySource", "1.0.0");

    public static Arbitrary<SdkLimitOptions> SdkLimitOptionsArbitrary()
    {
        var gen = from spanAttributesLimit in Gen.Choose(0, 1000).Select(x => (int?)x)
                  from spanEventsLimit in Gen.Choose(0, 1000).Select(x => (int?)x)
                  from spanLinksLimit in Gen.Choose(0, 1000).Select(x => (int?)x)
                  from spanEventAttributesLimit in Gen.Choose(0, 1000).Select(x => (int?)x)
                  from spanLinkAttributesLimit in Gen.Choose(0, 1000).Select(x => (int?)x)
                  from attributeValueLimit in Gen.Choose(0, 10000).Select(x => (int?)x)
                  from logAttributesLimit in Gen.Choose(0, 1000).Select(x => (int?)x)
                  select new SdkLimitOptions
                  {
                      AttributeValueLengthLimit = attributeValueLimit,
                      LogRecordAttributeCountLimit = logAttributesLimit,
                      SpanAttributeCountLimit = spanAttributesLimit,
                      SpanEventAttributeCountLimit = spanEventAttributesLimit,
                      SpanEventCountLimit = spanEventsLimit,
                      SpanLinkAttributeCountLimit = spanLinkAttributesLimit,
                      SpanLinkCountLimit = spanLinksLimit,
                  };

        return gen.ToArbitrary();
    }

    public static Arbitrary<Activity> ActivityArbitrary()
    {
        var gen = Gen.Sized(size =>
        {
            var activity = TestActivitySource.StartActivity($"TestActivity_{Guid.NewGuid():N}")!;
            if (activity == null)
            {
                return Gen.Constant(new Activity("Fallback"));
            }

            // Generate tags
            var tagCount = Math.Min(size, 50);
            for (int i = 0; i < tagCount; i++)
            {
                activity.SetTag($"tag.{i}", $"value_{i}_{Guid.NewGuid():N}");
            }

            // Generate events
            var eventCount = Math.Min(size / 10, 10);
            for (int i = 0; i < eventCount; i++)
            {
                var eventTags = new ActivityTagsCollection
                {
                    { $"event.tag.{i}", $"event_value_{i}" },
                };
                activity.AddEvent(new ActivityEvent($"Event_{i}", DateTimeOffset.UtcNow, eventTags));
            }

            // Generate links
            var linkCount = Math.Min(size / 10, 10);
            for (int i = 0; i < linkCount; i++)
            {
                var linkTags = new ActivityTagsCollection
                {
                    { $"link.tag.{i}", $"link_value_{i}" },
                };

                var context = new ActivityContext(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);

                activity.AddLink(new ActivityLink(context, linkTags));
            }

            // Set status
            var statusGen = Gen.Elements(ActivityStatusCode.Unset, ActivityStatusCode.Ok, ActivityStatusCode.Error);
            activity.SetStatus(statusGen.Sample(size, 1).First());

            return Gen.Constant(activity);
        });

        return gen.ToArbitrary();
    }

    public static Arbitrary<Resource> ResourceArbitrary()
    {
        var gen = Gen.Sized(size =>
        {
            var count = Math.Min(size, 20);
            var attributes = new Dictionary<string, object>(count);

            for (int i = 0; i < count; i++)
            {
                attributes[$"resource.attr.{i}"] = $"value_{i}";
            }

            return Gen.Constant(Resource.Empty.Merge(new Resource(attributes)));
        });

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates valid Metric instances for different metric types.
    /// </summary>
    public static Arbitrary<Batch<Metric>> BatchMetricArbitrary()
    {
        var gen = Gen.Sized(size =>
        {
            var metrics = new List<Metric>();

            var builder = Sdk.CreateMeterProviderBuilder()
                .AddMeter("FuzzTest.Meter")
                .AddInMemoryExporter(metrics);

            if (size % 7 == 0)
            {
                var boundaries = new double[Math.Min(size % 10, 10)];
                for (int i = 0; i < boundaries.Length; i++)
                {
                    boundaries[i] = (i * 50.0) + (size % 50);
                }

                builder.AddView("test.histogram", new ExplicitBucketHistogramConfiguration { Boundaries = boundaries });
            }

            using (var meterProvider = builder.Build())
            {
                using var meter = new Meter("FuzzTest.Meter", "1.0.0");

                var counter = meter.CreateCounter<long>("test.counter");

                for (int i = 0; i < size; i++)
                {
                    counter.Add(100 * i);
                }

                double gaugeValue = size;
                var gauge = meter.CreateObservableGauge("test.gauge", () => gaugeValue++);

                var histogram = meter.CreateHistogram<int>("test.histogram");

                for (int i = 0; i < size; i++)
                {
                    histogram.Record(200 * i);
                }

                meterProvider.ForceFlush();
            }

            var batch = new Batch<Metric>([.. metrics], metrics.Count);

            return Gen.Constant(batch);
        });

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates valid LogRecord instances.
    /// </summary>
    public static Arbitrary<LogRecord> LogRecordArbitrary()
    {
        var gen = Gen.Sized(size =>
        {
            var logRecord = LogRecordSharedPool.Current.Rent();

            logRecord.Timestamp = DateTime.UtcNow;
            logRecord.Severity = (LogRecordSeverity)(size % 7);

            // Add attributes
            var count = Math.Min(size, 50);
            var attributes = new List<KeyValuePair<string, object?>>(count);
            for (int i = 0; i < count; i++)
            {
                attributes.Add(new KeyValuePair<string, object?>($"log.attr.{i}", $"value_{i}"));
            }

            if (attributes.Count > 0)
            {
                logRecord.Attributes = attributes;
            }

            return Gen.Constant(logRecord);
        });

        return gen.ToArbitrary();
    }

    public static Arbitrary<Activity[]> ActivityBatchArbitrary()
    {
        var gen = Gen.Sized(size =>
        {
            var batchSize = Math.Min(size, 100);
            return Gen.ArrayOf(ActivityArbitrary().Generator, batchSize);
        });

        return gen.ToArbitrary();
    }

    public static Arbitrary<int> BufferSizeArbitrary() => Gen.Choose(64, 10 * 1024 * 1024).ToArbitrary();

    public static void RegisterAll()
    {
        SdkLimitOptionsArbitrary();
        ActivityArbitrary();
        ResourceArbitrary();
        LogRecordArbitrary();
        ActivityBatchArbitrary();
    }
}
