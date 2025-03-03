// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using OpenTracing;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

public class IntegrationTests
{
    private const string ChildActivitySource = "ChildActivitySource";
    private const string ParentActivitySource = "ParentActivitySource";

    [Theory]
    [InlineData(SamplingDecision.Drop, SamplingDecision.Drop, SamplingDecision.Drop)]
    [InlineData(SamplingDecision.Drop, SamplingDecision.RecordAndSample, SamplingDecision.Drop)]
    [InlineData(SamplingDecision.Drop, SamplingDecision.RecordOnly, SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly, SamplingDecision.RecordAndSample, SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample, SamplingDecision.RecordOnly, SamplingDecision.RecordAndSample)]
    [InlineData(SamplingDecision.RecordAndSample, SamplingDecision.Drop, SamplingDecision.RecordAndSample)]
    public void WithActivities(
        SamplingDecision parentActivitySamplingDecision,
        SamplingDecision shimSamplingDecision,
        SamplingDecision childActivitySamplingDecision)
    {
        var exportedSpans = new List<Activity>();

        const string ParentActivityName = "ParentActivity";
        const string ShimActivityName = "ShimActivity";
        const string ChildActivityName = "ChildActivity";

        var testSampler = new TestSampler((samplingParameters) =>
            samplingParameters.Name switch
            {
                ParentActivityName => parentActivitySamplingDecision,
                ShimActivityName => shimSamplingDecision,
                ChildActivityName => childActivitySamplingDecision,
                _ => SamplingDecision.Drop,
            });

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedSpans)
            .SetSampler(testSampler)
            .When(
                parentActivitySamplingDecision == SamplingDecision.RecordAndSample,
                b => b.AddSource(ParentActivitySource))
            .When(
                shimSamplingDecision == SamplingDecision.RecordAndSample,
                b => b.AddSource("opentracing-shim"))
            .When(
                childActivitySamplingDecision == SamplingDecision.RecordAndSample,
                b => b.AddSource(ChildActivitySource))
            .Build();

        var otTracer = new TracerShim(
            tracerProvider,
            Propagators.DefaultTextMapPropagator);

        // Real usage requires a call OpenTracing.Util.GlobalTracer.Register(otTracer),
        // however, that can only happen once per process, we don't do it here so we
        // can run multiple tests in the same process.

        using var parentActivitySource = new ActivitySource(ParentActivitySource);
        using var childActivitySource = new ActivitySource(ChildActivitySource);

        using (var parentActivity = parentActivitySource.StartActivity(ParentActivityName))
        {
            using (IScope parentScope = otTracer.BuildSpan(ShimActivityName).StartActive())
            {
                parentScope.Span.SetTag("parent", true);

                using var childActivity = childActivitySource.StartActivity(ChildActivityName);
            }
        }

        var expectedExportedSpans = new string?[]
            {
                childActivitySamplingDecision == SamplingDecision.RecordAndSample ? ChildActivityName : null,
                shimSamplingDecision == SamplingDecision.RecordAndSample ? ShimActivityName : null,
                parentActivitySamplingDecision == SamplingDecision.RecordAndSample ? ParentActivityName : null,
            }
            .Where(s => s is not null)
            .ToList();

        for (int i = 0; i < expectedExportedSpans.Count; i++)
        {
            Assert.Equal(expectedExportedSpans[i], exportedSpans[i].DisplayName);
        }

        if (childActivitySamplingDecision == SamplingDecision.RecordAndSample)
        {
            if (shimSamplingDecision == SamplingDecision.RecordAndSample)
            {
                Assert.Same(exportedSpans[1], exportedSpans[0].Parent);
            }
        }
    }

    private class TestSampler : Sampler
    {
        private readonly Func<SamplingParameters, SamplingDecision> shouldSampleDelegate;

        public TestSampler(Func<SamplingParameters, SamplingDecision> shouldSampleDelegate)
        {
            this.shouldSampleDelegate = shouldSampleDelegate;
        }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            return new SamplingResult(this.shouldSampleDelegate(samplingParameters));
        }
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Local use only")]
internal static class ConditionalTracerProviderBuilderExtension
{
    public static TracerProviderBuilder When(
        this TracerProviderBuilder builder,
        bool condition,
        Func<TracerProviderBuilder, TracerProviderBuilder> conditionalDelegate)
    {
        if (condition)
        {
            builder = conditionalDelegate(builder);
        }

        return builder;
    }
}
