// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Trace.Tests;

public class TracerTest : IDisposable
{
    private readonly ITestOutputHelper output;
    private readonly Tracer tracer;

    public TracerTest(ITestOutputHelper output)
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

        var test = TestingEngine.Create(config, InnerTest);

        test.Run();

        this.output.WriteLine(test.GetReport());
        this.output.WriteLine($"Bugs, if any: {string.Join("\n", test.TestReport.BugReports)}");

        var dir = Directory.GetCurrentDirectory();
        if (test.TryEmitReports(dir, $"{nameof(this.TracerConcurrencyTest)}_CoyoteOutput", out IEnumerable<string> reportPaths))
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

            Thread[] getTracerThreads = new Thread[testTracerProvider.ExpectedNumberOfThreads];
            for (int i = 0; i < testTracerProvider.ExpectedNumberOfThreads; i++)
            {
                getTracerThreads[i] = new Thread((object? state) =>
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

            testTracerProvider.Dispose();

            foreach (var getTracerThread in getTracerThreads)
            {
                getTracerThread.Join();
            }

            Assert.Empty(tracers);
        }
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

        using (var span = tracer.StartActiveSpan("TestSpan"))
        {
            // Activity started by the tracer with tags
        }

        var activity = Assert.Single(exportedItems);

        Assert.Contains(activity.Source.Tags!, kvp => kvp.Key == "tracerTag" && (string)kvp.Value! == "tracerValue");
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
    }
}
