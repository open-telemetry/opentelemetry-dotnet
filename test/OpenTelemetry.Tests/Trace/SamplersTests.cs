// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

public class SamplersTests
{
    private readonly ActivityTraceId traceId;
    private readonly ActivitySpanId parentSpanId;

    public SamplersTests()
    {
        this.traceId = ActivityTraceId.CreateRandom();
        this.parentSpanId = ActivitySpanId.CreateRandom();
    }

    [Theory]
    [InlineData(ActivityTraceFlags.Recorded)]
    [InlineData(ActivityTraceFlags.None)]
    public void AlwaysOnSampler_AlwaysReturnTrue(ActivityTraceFlags flags)
    {
        var parentContext = new ActivityContext(this.traceId, this.parentSpanId, flags);
        var link = new ActivityLink(parentContext);

        Assert.Equal(
            SamplingDecision.RecordAndSample,
            new AlwaysOnSampler().ShouldSample(new SamplingParameters(parentContext, this.traceId, "Another name", ActivityKind.Server, null, [link])).Decision);
    }

    [Fact]
    public void AlwaysOnSampler_GetDescription()
    {
        Assert.Equal("AlwaysOnSampler", new AlwaysOnSampler().Description);
    }

    [Theory]
    [InlineData(ActivityTraceFlags.Recorded)]
    [InlineData(ActivityTraceFlags.None)]
    public void AlwaysOffSampler_AlwaysReturnFalse(ActivityTraceFlags flags)
    {
        var parentContext = new ActivityContext(this.traceId, this.parentSpanId, flags);
        var link = new ActivityLink(parentContext);

        Assert.Equal(
            SamplingDecision.Drop,
            new AlwaysOffSampler().ShouldSample(new SamplingParameters(parentContext, this.traceId, "Another name", ActivityKind.Server, null, [link])).Decision);
    }

    [Fact]
    public void AlwaysOffSampler_GetDescription()
    {
        Assert.Equal("AlwaysOffSampler", new AlwaysOffSampler().Description);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void TracerProviderSdkSamplerAttributesAreAppliedToLegacyActivity(SamplingDecision samplingDecision)
    {
        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                var attributes = new Dictionary<string, object>
                {
                    { "tagkeybysampler", "tagvalueaddedbysampler" },
                };
                return new SamplingResult(samplingDecision, attributes);
            },
        };

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(testSampler)
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        using var activity = new Activity(operationNameForLegacyActivity);
        activity.Start();
        Assert.NotNull(activity);
        if (samplingDecision != SamplingDecision.Drop)
        {
            Assert.Contains(new KeyValuePair<string, object?>("tagkeybysampler", "tagvalueaddedbysampler"), activity.TagObjects);
        }

        activity.Stop();
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersCanModifyTraceStateOnLegacyActivity(SamplingDecision samplingDecision)
    {
        RunLegacyActivitySamplerTest(samplingDecision, samplerTraceState: "a=1,b=2,c=3,d=4");
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersDoesNotImpactTraceStateWhenUsingNullLegacyActivity(SamplingDecision samplingDecision)
    {
        RunLegacyActivitySamplerTest(samplingDecision, samplerTraceState: null);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersCanModifyTraceState(SamplingDecision sampling)
    {
        var parentTraceState = "a=1,b=2";
        var newTraceState = "a=1,b=2,c=3,d=4";
        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                if (samplingParams.Name == "root")
                {
                    Assert.Equal(parentTraceState, samplingParams.ParentContext.TraceState);
                }
                else
                {
                    Assert.Equal(newTraceState, samplingParams.ParentContext.TraceState);
                }

                return new SamplingResult(sampling, newTraceState);
            },
        };

        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(testSampler)
            .Build();

        // Note: Remote parent is set as recorded
        var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, parentTraceState, isRemote: true);

        using var root = activitySource.StartActivity("root", ActivityKind.Server, parentContext);

        // Note: We always create a root even for Drop. When dropping the
        // created root is for propagation only
        Assert.NotNull(root);
        Assert.Equal(newTraceState, root.TraceStateString);

        if (sampling == SamplingDecision.RecordAndSample)
        {
            Assert.True(root.Recorded);
            Assert.True(root.IsAllDataRequested);
        }
        else if (sampling == SamplingDecision.RecordOnly)
        {
            Assert.False(root.Recorded);
            Assert.True(root.IsAllDataRequested);
        }
        else
        {
            Assert.False(root.Recorded);
            Assert.False(root.IsAllDataRequested);
        }

        using var child = activitySource.StartActivity("child", ActivityKind.Server);

        if (sampling != SamplingDecision.Drop)
        {
            Assert.NotNull(child);
            Assert.Equal(newTraceState, child.TraceStateString);
        }
        else
        {
            Assert.Null(child);
        }
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersDoesNotImpactTraceStateWhenUsingNull(SamplingDecision sampling)
    {
        var parentTraceState = "a=1,b=2";
        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                Assert.Equal(parentTraceState, samplingParams.ParentContext.TraceState);

                // Not explicitly setting tracestate, leaving it null.
                // backward compat test that existing
                // samplers will not inadvertently
                // reset Tracestate
                return new SamplingResult(sampling);
            },
        };

        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(testSampler)
            .Build();

        var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, parentTraceState, true);

        using var activity = activitySource.StartActivity("root", ActivityKind.Server, parentContext);
        if (sampling != SamplingDecision.Drop)
        {
            Assert.NotNull(activity);
            Assert.Equal(parentTraceState, activity.TraceStateString);
        }
    }

    [Fact]
    public void SamplerExceptionBubblesUpTest()
    {
        // Note: This test verifies there is NO try/catch around sampling
        // and it will throw. For the discussion behind this see:
        // https://github.com/open-telemetry/opentelemetry-dotnet/pull/4072

        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(new ThrowingSampler())
            .Build();

        Assert.Throws<InvalidOperationException>(() => activitySource.StartActivity("ThrowingSampler"));
    }

    [Fact]
    public void SamplerShutdownInvokesOnShutdownOnce()
    {
        var sampler = new ShutdownTrackingSampler();

        Assert.True(sampler.Shutdown());
        Assert.False(sampler.Shutdown());
        Assert.False(sampler.Shutdown(1_000));

        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.Equal(Timeout.Infinite, sampler.LastTimeout);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(int.MinValue)]
    public void SamplerShutdownThrowsForInvalidTimeoutWithoutShuttingDown(int timeoutMilliseconds)
    {
        var sampler = new ShutdownTrackingSampler();

        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.Shutdown(timeoutMilliseconds));
        Assert.Equal(0, sampler.ShutdownCalls);

        Assert.True(sampler.Shutdown());
        Assert.Equal(1, sampler.ShutdownCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SamplerShutdownReturnsFalseWhenOnShutdownFails(bool throws)
    {
        var sampler = new ShutdownTrackingSampler
        {
            Result = false,
            Exception = throws ? new InvalidOperationException("test exception") : null,
        };

        Assert.False(sampler.Shutdown());
        Assert.Equal(1, sampler.ShutdownCalls);
    }

    [Fact]
    public void SamplerShutdownConcurrentCallsInvokeOnShutdownOnce()
    {
        const int threadCount = 8;

        var sampler = new ShutdownTrackingSampler();
        var successes = 0;

        using var start = new ManualResetEventSlim();

        // Dedicated threads, so that the calls are not serialized by thread pool starvation
        var threads = new Thread[threadCount];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                start.Wait();

                if (sampler.Shutdown())
                {
                    Interlocked.Increment(ref successes);
                }
            });

            threads[i].Start();
        }

        start.Set();

        foreach (var thread in threads)
        {
            thread.Join();
        }

        Assert.Equal(1, sampler.ShutdownCalls);
        Assert.Equal(1, successes);
    }

    [Fact]
    public void BuiltInSamplersShutdownSucceeds()
    {
        Assert.True(new AlwaysOnSampler().Shutdown());
        Assert.True(new AlwaysOffSampler().Shutdown());
        Assert.True(new TraceIdRatioBasedSampler(0.5).Shutdown());
        Assert.True(new ParentBasedSampler(new AlwaysOnSampler()).Shutdown());
        Assert.True(new AlwaysRecordSampler(new AlwaysOffSampler()).Shutdown());
    }

    [Fact]
    public void ParentBasedSamplerShutdownShutsDownRootSampler()
    {
        var root = new ShutdownTrackingSampler();

        Assert.True(new ParentBasedSampler(root).Shutdown());

        Assert.Equal(1, root.ShutdownCalls);
    }

    [Fact]
    public void ParentBasedSamplerShutdownShutsDownEachDelegateOnce()
    {
        var root = new ShutdownTrackingSampler();
        var shared = new ShutdownTrackingSampler();
        var local = new ShutdownTrackingSampler();

        var sampler = new ParentBasedSampler(
            root,
            remoteParentSampled: shared,
            remoteParentNotSampled: shared,
            localParentSampled: local,
            localParentNotSampled: root);

        Assert.True(sampler.Shutdown());
        Assert.False(sampler.Shutdown());

        Assert.Equal(1, root.ShutdownCalls);
        Assert.Equal(1, shared.ShutdownCalls);
        Assert.Equal(1, local.ShutdownCalls);
    }

    [Fact]
    public void ParentBasedSamplerShutdownDoesNotFailWhenDelegateWasAlreadyShutDownByAnotherDelegate()
    {
        // The leaf is shut down through the wrapper before the direct delegate is reached
        var leaf = new ShutdownTrackingSampler();
        var sampler = new ParentBasedSampler(new AlwaysRecordSampler(leaf), remoteParentSampled: leaf);

        Assert.True(sampler.Shutdown());

        Assert.Equal(1, leaf.ShutdownCalls);
    }

    [Fact]
    public void SharedSamplerShutdownReportsTheRecordedFailureToEveryCaller()
    {
        var failing = new ShutdownTrackingSampler { Result = false };

        // Only the first one to reach the sampler shuts it down, but all of them see that it failed
        Assert.False(new AlwaysRecordSampler(failing).Shutdown());
        Assert.False(new ParentBasedSampler(failing).Shutdown());
        Assert.False(new ParentBasedSampler(new AlwaysRecordSampler(failing)).Shutdown());

        Assert.Equal(1, failing.ShutdownCalls);
    }

    [Fact]
    public void SharedSamplerShutdownDoesNotReportSuccessWhileAnotherShutdownIsRunning()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var shared = new ShutdownTrackingSampler
        {
            Callback = () =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30));
            },
        };

        var firstResult = false;
        var waitingResult = false;

        var first = new Thread(() => firstResult = new AlwaysRecordSampler(shared).Shutdown());
        var waiting = new Thread(() => waitingResult = new AlwaysRecordSampler(shared).Shutdown());

        first.Start();

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

            // Not finished within the timeout
            Assert.False(new AlwaysRecordSampler(shared).Shutdown(50));

            // Waits for the shutdown which is running, rather than reporting its own result
            waiting.Start();
        }
        finally
        {
            release.Set();
            first.Join();

            if (waiting.ThreadState != System.Threading.ThreadState.Unstarted)
            {
                waiting.Join();
            }
        }

        Assert.True(firstResult);
        Assert.True(waitingResult);
        Assert.Equal(1, shared.ShutdownCalls);

        // Once finished, the outcome is reported without waiting
        Assert.True(new AlwaysRecordSampler(shared).Shutdown(0));
    }

    [Fact]
    public void ParentBasedSamplerShutdownShutsDownAllDelegatesWhenOneFails()
    {
        var root = new ShutdownTrackingSampler { Result = false };
        var remote = new ShutdownTrackingSampler();
        var local = new ShutdownTrackingSampler { Exception = new InvalidOperationException("test exception") };

        var sampler = new ParentBasedSampler(root, remote, localParentSampled: local);

        Assert.False(sampler.Shutdown());

        Assert.Equal(1, root.ShutdownCalls);
        Assert.Equal(1, remote.ShutdownCalls);
        Assert.Equal(1, local.ShutdownCalls);
    }

    [Fact]
    public void ParentBasedSamplerShutdownDoesNotShutDownSharedBuiltInSamplers()
    {
        // The defaults are shared by every provider, so shutting one
        // sampler down must not make the next one report a failure.
        Assert.True(new ParentBasedSampler(AlwaysOnSampler.Instance).Shutdown());
        Assert.True(new ParentBasedSampler(AlwaysOnSampler.Instance).Shutdown());
        Assert.True(new AlwaysRecordSampler(AlwaysOnSampler.Instance).Shutdown());
        Assert.True(new AlwaysRecordSampler(AlwaysOnSampler.Instance).Shutdown());
    }

    [Fact]
    public void ParentBasedSamplerShutdownSharesTimeoutBetweenDelegates()
    {
        var root = new ShutdownTrackingSampler();
        var local = new ShutdownTrackingSampler();

        Assert.True(new ParentBasedSampler(root, localParentSampled: local).Shutdown(60_000));

        Assert.InRange(root.LastTimeout, 1, 60_000);
        Assert.InRange(local.LastTimeout, 1, root.LastTimeout);

        root = new ShutdownTrackingSampler();

        Assert.True(new ParentBasedSampler(root).Shutdown());
        Assert.Equal(Timeout.Infinite, root.LastTimeout);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AlwaysRecordSamplerShutdownShutsDownRootSampler(bool succeeds)
    {
        var root = new ShutdownTrackingSampler { Result = succeeds };
        var sampler = new AlwaysRecordSampler(root);

        Assert.Equal(succeeds, sampler.Shutdown(5_000));
        Assert.False(sampler.Shutdown(5_000));

        Assert.Equal(1, root.ShutdownCalls);
        Assert.Equal(5_000, root.LastTimeout);
    }

    private static void RunLegacyActivitySamplerTest(SamplingDecision samplingDecision, string? samplerTraceState)
    {
        var existingTraceState = "a=1,b=2";

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                Assert.Equal(samplingParams.Name, operationNameForLegacyActivity);
                Assert.Equal(existingTraceState, samplingParams.ParentContext.TraceState);
                return new SamplingResult(samplingDecision, samplerTraceState);
            },
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSampler)
            .AddLegacySource(operationNameForLegacyActivity)
            .Build();

        using var parentActivity = new Activity("Foo");
        parentActivity.TraceStateString = existingTraceState;
        parentActivity.Start();

        using var childActivity = new Activity(operationNameForLegacyActivity);
        childActivity.Start();

        if (samplerTraceState != null)
        {
            Assert.Equal(samplerTraceState, childActivity.TraceStateString);
        }
        else
        {
            Assert.Equal(existingTraceState, childActivity.TraceStateString);
        }

        if (samplingDecision == SamplingDecision.RecordAndSample)
        {
            Assert.True(childActivity.Recorded);
            Assert.True(childActivity.IsAllDataRequested);
        }
        else if (samplingDecision == SamplingDecision.RecordOnly)
        {
            Assert.False(childActivity.Recorded);
            Assert.True(childActivity.IsAllDataRequested);
        }
        else
        {
            Assert.False(childActivity.Recorded);
            Assert.False(childActivity.IsAllDataRequested);
        }
    }

    private sealed class ThrowingSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            throw new InvalidOperationException("ThrowingSampler");
        }
    }

    private sealed class ShutdownTrackingSampler : Sampler
    {
        private int shutdownCalls;

        public bool Result { get; set; } = true;

        public Exception? Exception { get; set; }

        public Action? Callback { get; set; }

        public int ShutdownCalls => Volatile.Read(ref this.shutdownCalls);

        public int LastTimeout { get; private set; }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new(SamplingDecision.RecordAndSample);

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            Interlocked.Increment(ref this.shutdownCalls);
            this.LastTimeout = timeoutMilliseconds;
            this.Callback?.Invoke();

            return this.Exception is { } exception ? throw exception : this.Result;
        }
    }
}
