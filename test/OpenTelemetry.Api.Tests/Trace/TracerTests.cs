// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

public sealed class TracerTests : IDisposable
{
    private readonly ITestOutputHelper output;
    private readonly Tracer tracer;

    public TracerTests(ITestOutputHelper output)
    {
        this.output = output;
        this.tracer = TracerProvider.Default.GetTracer("tracername", "tracerversion");
    }

    [Fact]
    public void CurrentSpanNullByDefault()
    {
        var current = Tracer.CurrentSpan;
        Assert.True(IsNoopSpan(current));
        Assert.False(current.Context.IsValid);
    }

    [Fact]
    public void TracerStartWithSpan()
    {
        Tracer.WithSpan(TelemetrySpan.NoopInstance);
        var current = Tracer.CurrentSpan;
        Assert.Same(current, TelemetrySpan.NoopInstance);
    }

    [Fact]
    public void TracerStartReturnsNoopSpanWhenNoSdk()
    {
        var span = this.tracer.StartSpan("name");
        Assert.True(IsNoopSpan(span));
        Assert.False(span.Context.IsValid);
        Assert.False(span.IsRecording);
    }

    [Fact]
    public void Tracer_StartRootSpan_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartRootSpan(null!);
        Assert.NotNull(span1.Activity);
        Assert.True(string.IsNullOrEmpty(span1.Activity.DisplayName));

        var span2 = this.tracer.StartRootSpan(null!, SpanKind.Client);
        Assert.NotNull(span2.Activity);
        Assert.True(string.IsNullOrEmpty(span2.Activity.DisplayName));

        var span3 = this.tracer.StartRootSpan(null!, SpanKind.Client, default);
        Assert.NotNull(span3.Activity);
        Assert.True(string.IsNullOrEmpty(span3.Activity.DisplayName));
    }

    [Fact]
    public async Task Tracer_StartRootSpan_StartsNewTrace()
    {
        var exportedItems = new List<Activity>();

        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .AddInMemoryExporter(exportedItems)
            .Build();

        async Task DoSomeAsyncWork()
        {
            await Task.Delay(10);
            using (tracer.GetTracer("tracername").StartRootSpan("RootSpan2"))
            {
                await Task.Delay(10);
            }
        }

        using (tracer.GetTracer("tracername").StartActiveSpan("RootSpan1"))
        {
            await DoSomeAsyncWork();
        }

        Assert.Equal(2, exportedItems.Count);

        var rootSpan2 = exportedItems[0];
        var rootSpan1 = exportedItems[1];
        Assert.Equal("RootSpan2", rootSpan2.DisplayName);
        Assert.Equal("RootSpan1", rootSpan1.DisplayName);
        Assert.Equal(default, rootSpan1.ParentSpanId);

        // This is where this test currently fails
        // rootSpan2 should be a root span of a new trace and not a child of rootSpan1
        Assert.Equal(default, rootSpan2.ParentSpanId);
        Assert.NotEqual(rootSpan2.TraceId, rootSpan1.TraceId);
        Assert.NotEqual(rootSpan2.ParentSpanId, rootSpan1.SpanId);
    }

    [Fact]
    public void Tracer_StartSpan_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartSpan(null!);
        Assert.NotNull(span1.Activity);
        Assert.True(string.IsNullOrEmpty(span1.Activity.DisplayName));

        var span2 = this.tracer.StartSpan(null!, SpanKind.Client);
        Assert.NotNull(span2.Activity);
        Assert.True(string.IsNullOrEmpty(span2.Activity.DisplayName));

        var span3 = this.tracer.StartSpan(null!, SpanKind.Client, null);
        Assert.NotNull(span3.Activity);
        Assert.True(string.IsNullOrEmpty(span3.Activity.DisplayName));
    }

    [Fact]
    public void Tracer_StartActiveSpan_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartActiveSpan(null!);
        Assert.NotNull(span1.Activity);
        Assert.True(string.IsNullOrEmpty(span1.Activity.DisplayName));

        var span2 = this.tracer.StartActiveSpan(null!, SpanKind.Client);
        Assert.NotNull(span2.Activity);
        Assert.True(string.IsNullOrEmpty(span2.Activity.DisplayName));

        var span3 = this.tracer.StartActiveSpan(null!, SpanKind.Client, null);
        Assert.NotNull(span3.Activity);
        Assert.True(string.IsNullOrEmpty(span3.Activity.DisplayName));
    }

    [Fact]
    public void Tracer_StartSpan_FromParent_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartSpan(null!, SpanKind.Client, TelemetrySpan.NoopInstance);
        Assert.NotNull(span1.Activity);
        Assert.True(string.IsNullOrEmpty(span1.Activity.DisplayName));

        var span2 = this.tracer.StartSpan(null!, SpanKind.Client, TelemetrySpan.NoopInstance, default);
        Assert.NotNull(span2.Activity);
        Assert.True(string.IsNullOrEmpty(span2.Activity.DisplayName));
    }

    [Fact]
    public void Tracer_StartSpan_FromParentContext_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var blankContext = default(SpanContext);

        var span1 = this.tracer.StartSpan(null!, SpanKind.Client, blankContext);
        Assert.NotNull(span1.Activity);
        Assert.True(string.IsNullOrEmpty(span1.Activity.DisplayName));

        var span2 = this.tracer.StartSpan(null!, SpanKind.Client, blankContext, default);
        Assert.NotNull(span2.Activity);
        Assert.True(string.IsNullOrEmpty(span2.Activity.DisplayName));
    }

    [Fact]
    public void Tracer_StartActiveSpan_FromParent_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartActiveSpan(null!, SpanKind.Client, TelemetrySpan.NoopInstance);
        Assert.NotNull(span1.Activity);
        Assert.True(string.IsNullOrEmpty(span1.Activity.DisplayName));

        var span2 = this.tracer.StartActiveSpan(null!, SpanKind.Client, TelemetrySpan.NoopInstance, default);
        Assert.NotNull(span2.Activity);
        Assert.True(string.IsNullOrEmpty(span2.Activity.DisplayName));
    }

    [Fact]
    public void Tracer_StartActiveSpan_FromParentContext_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var blankContext = default(SpanContext);

        var span1 = this.tracer.StartActiveSpan(null!, SpanKind.Client, blankContext);
        Assert.NotNull(span1.Activity);
        Assert.True(string.IsNullOrEmpty(span1.Activity.DisplayName));

        var span2 = this.tracer.StartActiveSpan(null!, SpanKind.Client, blankContext, default);
        Assert.NotNull(span2.Activity);
        Assert.True(string.IsNullOrEmpty(span2.Activity.DisplayName));
    }

    [Fact]
    public void Tracer_StartActiveSpan_CreatesActiveSpan()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartActiveSpan("Test");
        Assert.NotNull(span1.Activity);
        Assert.Equal(span1.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

        var span2 = this.tracer.StartActiveSpan("Test", SpanKind.Client);
        Assert.NotNull(span2.Activity);
        Assert.Equal(span2.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

        var span = this.tracer.StartSpan("foo");
        Tracer.WithSpan(span);

        var span3 = this.tracer.StartActiveSpan("Test", SpanKind.Client, span);
        Assert.NotNull(span3.Activity);
        Assert.Equal(span3.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

        var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var span4 = this.tracer.StartActiveSpan("Test", SpanKind.Client, spanContext);
        Assert.NotNull(span4.Activity);
        Assert.Equal(span4.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);
    }

    [Fact]
    public void GetCurrentSpanBlank()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();
        Assert.False(Tracer.CurrentSpan.Context.IsValid);
    }

    [Fact]
    public void GetCurrentSpanBlankWontThrowOnEnd()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();
        var current = Tracer.CurrentSpan;
        current.End();
    }

    [Fact]
    public void GetCurrentSpan()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span = this.tracer.StartSpan("foo");
        Tracer.WithSpan(span);

        Assert.Equal(span.Context.SpanId, Tracer.CurrentSpan.Context.SpanId);
        Assert.True(Tracer.CurrentSpan.Context.IsValid);
    }

    [Fact]
    public void CreateSpan_Sampled()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();
        var span = this.tracer.StartSpan("foo");
        Assert.True(span.IsRecording);
    }

    [Fact]
    public void CreateSpan_NotSampled()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .SetSampler(new AlwaysOffSampler())
            .Build();

        var span = this.tracer.StartSpan("foo");
        Assert.False(span.IsRecording);
    }

    [Fact]
    public void TracerBecomesNoopWhenParentProviderIsDisposedTest()
    {
        TracerProvider? provider;
        Tracer? tracer1;

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddSource("mytracer")
                   .Build())
        {
            provider = tracerProvider;
            tracer1 = tracerProvider.GetTracer("mytracer");

            var span1 = tracer1.StartSpan("foo");
            Assert.True(span1.IsRecording);
        }

        var span2 = tracer1.StartSpan("foo");
        Assert.False(span2.IsRecording);

        var tracer2 = provider.GetTracer("mytracer");

        var span3 = tracer2.StartSpan("foo");
        Assert.False(span3.IsRecording);
    }

    [SkipUnlessEnvVarFoundFact("OTEL_RUN_COYOTE_TESTS")]
    [Trait("CategoryName", "CoyoteConcurrencyTests")]
    public void TracerConcurrencyTest()
    {
        var config = Configuration.Create()
            .WithTestingIterations(100)
            .WithMemoryAccessRaceCheckingEnabled(true);

        using var test = TestingEngine.Create(config, InnerTest);

        test.Run();

        this.output.WriteLine(test.GetReport());
        this.output.WriteLine($"Bugs, if any: {string.Join("\n", test.TestReport.BugReports)}");

        var dir = Directory.GetCurrentDirectory();
        if (test.TryEmitReports(dir, $"{nameof(this.TracerConcurrencyTest)}_CoyoteOutput", out var reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                this.output.WriteLine($"Execution Report: {reportPath}");
            }
        }

        if (test.TryEmitCoverageReports(dir, $"{nameof(this.TracerConcurrencyTest)}_CoyoteOutput", out reportPaths))
        {
            foreach (var reportPath in reportPaths)
            {
                this.output.WriteLine($"Coverage report: {reportPath}");
            }
        }

        Assert.Equal(0, test.TestReport.NumOfFoundBugs);

        static void InnerTest()
        {
            var testTracerProvider = new TestTracerProvider
            {
                ExpectedNumberOfThreads = Math.Max(1, Environment.ProcessorCount / 2),
            };

            var tracers = testTracerProvider.Tracers;

            Assert.NotNull(tracers);

            var getTracerThreads = new Thread[testTracerProvider.ExpectedNumberOfThreads];
            for (var i = 0; i < testTracerProvider.ExpectedNumberOfThreads; i++)
            {
                getTracerThreads[i] = new Thread(state =>
                {
                    var testTracerProvider = state as TestTracerProvider;
                    Assert.NotNull(testTracerProvider);

                    var id = Interlocked.Increment(ref testTracerProvider.NumberOfThreads);
                    var name = $"Tracer{id}";

                    if (id == testTracerProvider.ExpectedNumberOfThreads)
                    {
                        testTracerProvider.StartHandle.Set();
                    }
                    else
                    {
                        testTracerProvider.StartHandle.WaitOne();
                    }

                    var tracer = testTracerProvider.GetTracer(name);

                    Assert.NotNull(tracer);
                });

                getTracerThreads[i].Start(testTracerProvider);
            }

            testTracerProvider.StartHandle.WaitOne();

            foreach (var getTracerThread in getTracerThreads)
            {
                getTracerThread.Join();
            }

            testTracerProvider.Dispose();

            Assert.Empty(tracers);
        }
    }

    [Fact]
    public void GetTracer_WithSameSchemaUrl_ReturnsSameInstance()
    {
        var schemaUrl = "http://schema";

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", schemaUrl);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", schemaUrl);

        Assert.Same(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithDifferentSchemaUrls_ReturnsDifferentInstances()
    {
        var schemaUrl1 = "http://schema1";
        var schemaUrl2 = "http://schema2";

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", schemaUrl1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", schemaUrl2);

        Assert.NotSame(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithSchemaUrl_AppliesSchemaUrlToActivities()
    {
        var exportedItems = new List<Activity>();
        var schemaUrl = "http://schema";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("test")
            .AddInMemoryExporter(exportedItems)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        var tracer = tracerProvider.GetTracer("test", "1.0.0", schemaUrl);

        using (tracer.StartActiveSpan("TestSpan"))
        {
            // Activity started by the tracer with schema URL
        }

        var activity = Assert.Single(exportedItems);

        Assert.Equal(schemaUrl, activity.Source.TelemetrySchemaUrl);
    }

    [Fact]
    public void GetTracer_WithSameTags_ReturnsSameInstance()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("tag1", "value1"), new("tag2", "value2") };
        var tags2 = new List<KeyValuePair<string, object?>> { new("tag1", "value1"), new("tag2", "value2") };

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags2);

        Assert.Same(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithEqualArrayTags_ReturnsSameInstance()
    {
        using var tracerProvider = new TestTracerProvider();
        Tracer? firstTracer = null;

        for (var i = 0; i < 10; i++)
        {
            // Each lookup supplies newly allocated arrays with the same values.
#pragma warning disable CA1825 // The cache must also match distinct empty arrays.
#pragma warning disable CA1861 // Each lookup must supply fresh arrays to exercise value equality.
            KeyValuePair<string, object?>[] tags =
            [
                new("strings", new string?[] { "eu", null, "us" }),
                new("booleans", new[] { true, false }),
                new("integers", new[] { 1, 2 }),
                new("longs", new[] { 1L, 2L }),
                new("doubles", new[] { 1.0, double.NaN }),
                new("floats", new[] { 1.0f, 2.0f }),
                new("bytes", new byte[] { 1, 2 }),
                new("shorts", new short[] { 1, 2 }),
                new("chars", new[] { 'a', 'b' }),
                new("empty", new string[0]),
            ];
#pragma warning restore CA1861 // Each lookup must supply fresh arrays to exercise value equality.
#pragma warning restore CA1825 // The cache must also match distinct empty arrays.

            var tracer = tracerProvider.GetTracer("test", "1.0.0", tags);
            firstTracer ??= tracer;
            Assert.Same(firstTracer, tracer);
        }

        Assert.Single(tracerProvider.Tracers!);
    }

    [Theory]
    [InlineData(new[] { "eu", "us" }, new[] { "eu", "uk" })]
    [InlineData(new[] { "eu", "us" }, new[] { "us", "eu" })]
    [InlineData(new[] { "eu", "us" }, new[] { "eu" })]
    [InlineData(new[] { "eu" }, "eu")]
    [InlineData(new[] { "eu" }, null)]
    [InlineData(new[] { 1, 2 }, new[] { 1L, 2L })]
    [InlineData(new int[0], new long[0])]
    public void GetTracer_WithDifferentArrayTags_ReturnsDifferentInstances(object? value1, object? value2)
    {
        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags: [new("regions", value1)]);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags: [new("regions", value2)]);

        Assert.NotSame(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithArrayTags_SnapshotsCallerValues()
    {
        using var tracerProvider = new TestTracerProvider();
        var value = new[] { "eu", "us" };
        KeyValuePair<string, object?>[] tags = [new("regions", value)];
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags);

        value[1] = "uk";
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags);

        value[1] = "us";
        var tracer3 = tracerProvider.GetTracer("test", "1.0.0", tags);

        Assert.NotSame(tracer1, tracer2);
        Assert.Same(tracer1, tracer3);
        Assert.Equal(2, tracerProvider.Tracers!.Count);

        var sourceValue = Assert.IsType<string[]>(Assert.Single(tracer1.ActivitySource!.Tags!).Value);
        Assert.NotSame(value, sourceValue);
        Assert.Equal("eu", sourceValue[0]);
        Assert.Equal("us", sourceValue[1]);
    }

    [Fact]
    public void GetTracer_WithArrayTags_SourceMutationDoesNotChangeCacheKey()
    {
        using var tracerProvider = new TestTracerProvider();
        var value = new[] { "eu", "us" };
        KeyValuePair<string, object?>[] tags = [new("regions", value)];
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags);

        var sourceValue = Assert.IsType<string[]>(Assert.Single(tracer1.ActivitySource!.Tags!).Value);
        sourceValue[1] = "uk";

        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags);

        Assert.Same(tracer1, tracer2);
        Assert.Single(tracerProvider.Tracers!);
        Assert.Equal("us", value[1]);
    }

    [Fact]
    public void GetTracer_WithMultidimensionalArrayTags_PreservesShape()
    {
        using var tracerProvider = new TestTracerProvider();
        var value = Array.CreateInstance(typeof(int), 1, 2);
        value.SetValue(1, 0, 0);
        value.SetValue(2, 0, 1);
        var differentShape = Array.CreateInstance(typeof(int), 2, 1);
        differentShape.SetValue(1, 0, 0);
        differentShape.SetValue(2, 1, 0);
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags: [new("values", value)]);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags: [new("values", value.Clone())]);
        var tracer3 = tracerProvider.GetTracer("test", "1.0.0", tags: [new("values", differentShape)]);

        Assert.Same(tracer1, tracer2);
        Assert.NotSame(tracer1, tracer3);
    }

    [Fact]
    public void GetTracer_WithSelfReferentialArrayTag_DoesNotRecurse()
    {
        using var tracerProvider = new TestTracerProvider();
        var value = new object[1];
        value[0] = value;

        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags: [new("values", value)]);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags: [new("values", value)]);

        Assert.Same(tracer1, tracer2);
        Assert.Single(tracerProvider.Tracers!);
    }

    [Fact]
    public void GetTracer_WithoutTags_ReturnsSameInstance()
    {
        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0");
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0");

        Assert.Same(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithDifferentTags_ReturnsDifferentInstances()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("tag1", "value1") };
        var tags2 = new List<KeyValuePair<string, object?>> { new("tag2", "value2") };

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags2);

        Assert.NotSame(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithDifferentOrderTags_ReturnsSameInstance()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("tag2", "value2"), new("tag1", "value1"), };
        var tags2 = new List<KeyValuePair<string, object?>> { new("tag1", "value1"), new("tag2", "value2"), };

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags2);

        Assert.Same(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_TagsValuesAreIntType_ReturnsSameInstance()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("tag2", 2), new("tag1", 1) };
        var tags2 = new List<KeyValuePair<string, object?>> { new("tag1", 1), new("tag2", 2) };

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags2);

        Assert.Same(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_TagsValuesAreSameWithDifferentOrder_ReturnsSameInstance()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("tag3", 1), new("tag1", 1), new("tag2", 1), new("tag1", 2), new("tag2", 2) };
        var tags2 = new List<KeyValuePair<string, object?>> { new("tag2", 1), new("tag1", 2), new("tag1", 1), new("tag2", 2), new("tag3", 1) };

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags2);

        Assert.Same(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_TagsContainNullValues_ReturnsSameInstance()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("tag3", 1), new("tag2", 3), new("tag1", null), new("tag2", null), new("tag1", 2), new("tag2", 2) };
        var tags2 = new List<KeyValuePair<string, object?>> { new("tag2", null), new("tag1", 2), new("tag2", 3), new("tag1", null), new("tag2", 2), new("tag3", 1) };

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags2);

        Assert.Same(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithDifferentTagsSize_ReturnsDifferentInstances()
    {
        var tags1 = new List<KeyValuePair<string, object?>> { new("tag2", 2), new("tag1", 1) };
        var tags2 = new List<KeyValuePair<string, object?>> { new("tag1", 1), new("tag2", 2), new("tag3", 3) };

        using var tracerProvider = new TestTracerProvider();
        var tracer1 = tracerProvider.GetTracer("test", "1.0.0", tags1);
        var tracer2 = tracerProvider.GetTracer("test", "1.0.0", tags2);

        Assert.NotSame(tracer1, tracer2);
    }

    [Fact]
    public void GetTracer_WithTagsAndWithoutTags_ReturnsDifferentInstances()
    {
        var tags = new List<KeyValuePair<string, object?>> { new("tag1", "value1") };

        using var tracerProvider = new TestTracerProvider();
        var tracerWithTags = tracerProvider.GetTracer("test", "1.0.0", tags);
        var tracerWithoutTags = tracerProvider.GetTracer("test", "1.0.0");

        Assert.NotEqual(tracerWithTags, tracerWithoutTags);
    }

    [Fact]
    public void GetTracer_WithTags_AppliesTagsToActivities()
    {
        var exportedItems = new List<Activity>();
        var tags = new List<KeyValuePair<string, object?>> { new("tracerTag", "tracerValue") };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("test")
            .AddInMemoryExporter(exportedItems)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        var tracer = tracerProvider.GetTracer("test", "1.0.0", tags);

        using (tracer.StartActiveSpan("TestSpan"))
        {
            // Activity started by the tracer with tags
        }

        var activity = Assert.Single(exportedItems);

        Assert.Contains(activity.Source.Tags!, kvp => kvp.Key == "tracerTag" && (string)kvp.Value! == "tracerValue");
    }

    [Fact]
    public void StartSpan_WhenCurrentActivityStoppedOnAnotherThread_DoesNotLeaveNewSpanAsCurrent()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .SetSampler(new AlwaysOnSampler())
            .Build();

        var tracer = tracerProvider.GetTracer("tracername");

        using var activeSpan = tracer.StartActiveSpan("previous");
        var previousActivity = Activity.Current;
        Assert.NotNull(previousActivity);

        var thread = new Thread(previousActivity.Stop);
        thread.Start();
        thread.Join();

        Assert.True(previousActivity.IsStopped);
        Assert.Same(previousActivity, Activity.Current);

        using var newSpan = tracer.StartSpan("new span");

        Assert.Null(Activity.Current);
    }

    [Fact]
    public void StartSpan_WhenCurrentActivityStoppedOnAnotherThread_RestoresFirstRunningAncestor()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .SetSampler(new AlwaysOnSampler())
            .Build();

        var tracer = tracerProvider.GetTracer("tracername");

        using var grandparentSpan = tracer.StartActiveSpan("grandparent");
        var grandparentActivity = Activity.Current;
        Assert.NotNull(grandparentActivity);

        using var parentSpan = tracer.StartActiveSpan("parent");
        var parentActivity = Activity.Current;
        Assert.NotNull(parentActivity);

        using var currentSpan = tracer.StartActiveSpan("current");
        var currentActivity = Activity.Current;
        Assert.NotNull(currentActivity);

        Assert.Same(grandparentActivity, parentActivity.Parent);
        Assert.Same(parentActivity, currentActivity.Parent);

        var thread = new Thread(currentActivity.Stop);
        thread.Start();
        thread.Join();

        Assert.True(currentActivity.IsStopped);
        Assert.Same(currentActivity, Activity.Current);

        using var newSpan = tracer.StartSpan("new span");

        Assert.Same(parentActivity, Activity.Current);
    }

    [Fact]
    public void StartSpan_WhenMultipleAncestorsStoppedOnAnotherThread_RestoresFirstRunningAncestor()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .SetSampler(new AlwaysOnSampler())
            .Build();

        var tracer = tracerProvider.GetTracer("tracername");

        using var grandparentSpan = tracer.StartActiveSpan("grandparent");
        var grandparentActivity = Activity.Current;
        Assert.NotNull(grandparentActivity);

        using var parentSpan = tracer.StartActiveSpan("parent");
        var parentActivity = Activity.Current;
        Assert.NotNull(parentActivity);

        using var currentSpan = tracer.StartActiveSpan("current");
        var currentActivity = Activity.Current;
        Assert.NotNull(currentActivity);

        Assert.Same(grandparentActivity, parentActivity.Parent);
        Assert.Same(parentActivity, currentActivity.Parent);

        var thread = new Thread(() =>
        {
            currentActivity.Stop();
            parentActivity.Stop();
        });
        thread.Start();
        thread.Join();

        Assert.True(currentActivity.IsStopped);
        Assert.True(parentActivity.IsStopped);
        Assert.Same(currentActivity, Activity.Current);

        using var newSpan = tracer.StartSpan("new span");

        Assert.Same(grandparentActivity, Activity.Current);
    }

    [Fact]
    public void StartRootSpan_WhenCurrentActivityStoppedOnAnotherThread_DoesNotLeaveNewSpanAsCurrent()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .SetSampler(new AlwaysOnSampler())
            .Build();

        var tracer = tracerProvider.GetTracer("tracername");

        using var activeSpan = tracer.StartActiveSpan("previous");
        var previousActivity = Activity.Current;
        Assert.NotNull(previousActivity);

        var thread = new Thread(previousActivity.Stop);
        thread.Start();
        thread.Join();

        Assert.True(previousActivity.IsStopped);
        Assert.Same(previousActivity, Activity.Current);

        using var newSpan = tracer.StartRootSpan("new root span");

        Assert.Null(Activity.Current);
    }

    public void Dispose()
    {
        Activity.Current = null;
        GC.SuppressFinalize(this);
    }

    private static bool IsNoopSpan(TelemetrySpan span)
    {
        return span.Activity == null;
    }

    private sealed class TestTracerProvider : TracerProvider
    {
        public int ExpectedNumberOfThreads;
        public int NumberOfThreads;
        public EventWaitHandle StartHandle = new ManualResetEvent(false);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.StartHandle.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
