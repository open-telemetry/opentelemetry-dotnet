// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public sealed class TracerProviderSdkEventSourceTests
{
    public TracerProviderSdkEventSourceTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    }

    [Fact]
    public void SdkEmitsDiagnosticEventWhenActivityDroppedDueToUnsampledLocalParent()
    {
        using var source = new ActivitySource(Utils.GetCurrentMethodName());

        // Default sampler is ParentBased(AlwaysOn).
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .Build();

        // Simulate the framework-created in-process parent activity. Its source is
        // not registered with the provider, so it is never sampled (Recorded == false)
        // but it still becomes Activity.Current.
        using var parent = new Activity("ParentNotRecorded");
        parent.Start();

        Assert.False(parent.Recorded);
        Assert.False(parent.Context.IsRemote);

        // Create the event listener after the parent has started so its thread
        // activity id is the one stamped on the diagnostic event.
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using (var child = source.StartActivity("Child"))
        {
            // The child is dropped because its local parent is not recorded.
            Assert.Null(child);
        }

        parent.Stop();

        var droppedEvents = eventListener.Messages.Where(
            e => e.EventId == 58 &&
                 (e.Payload?.Count ?? 0) >= 2 &&
                 (e.Payload![0] as string) == "Child" &&
                 (e.Payload![1] as string) == source.Name);

        Assert.Single(droppedEvents);
    }

    [Fact]
    public void SdkDoesNotEmitUnsampledLocalParentEventForRemoteParent()
    {
        // A remote parent's sampling decision is controlled by the caller and is
        // expected to flow through, so the diagnostic event should not be emitted.
        using var source = new ActivitySource(Utils.GetCurrentMethodName());

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .Build();

        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var remoteParent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            isRemote: true);

        using (var child = source.StartActivity("Child", ActivityKind.Internal, remoteParent))
        {
            // Dropped as PropagationData by the default ParentBased sampler.
            Assert.False(child?.Recorded ?? false);
        }

        var droppedEvents = eventListener.Messages.Where(
            e => e.EventId == 58 && (e.Payload?.Count ?? 0) >= 2 && (e.Payload![0] as string) == "Child");

        Assert.Empty(droppedEvents);
    }

    [Fact]
    public void SdkDoesNotEmitUnsampledLocalParentEventForNonParentBasedSampler()
    {
        using var source = new ActivitySource(Utils.GetCurrentMethodName());

        var sampler = new TestSampler
        {
            SamplingAction = _ => new SamplingResult(SamplingDecision.Drop),
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .SetSampler(sampler)
            .Build();

        using var parent = new Activity("ParentNotRecorded");
        parent.Start();

        Assert.False(parent.Recorded);
        Assert.False(parent.Context.IsRemote);

        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using (var child = source.StartActivity("Child"))
        {
            Assert.Null(child);
        }

        parent.Stop();

        var droppedEvents = eventListener.Messages.Where(
            e => e.EventId == 58 && (e.Payload?.Count ?? 0) >= 2 && (e.Payload![0] as string) == "Child");

        Assert.Empty(droppedEvents);
    }
}
