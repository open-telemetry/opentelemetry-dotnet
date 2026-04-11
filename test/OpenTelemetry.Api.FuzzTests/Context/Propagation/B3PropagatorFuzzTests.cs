// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Api.FuzzTests;

[Obsolete("B3Propagator is obsolete but intentionally fuzz tested.")]
public class B3PropagatorFuzzTests
{
    private const int MaxTests = 200;

    private const string CombinedHeader = "b3";
    private const string TraceIdHeader = "X-B3-TraceId";
    private const string SpanIdHeader = "X-B3-SpanId";
    private const string SampledHeader = "X-B3-Sampled";

    [Property(MaxTest = MaxTests)]
    public Property MultipleHeaderInjectExtractRoundTripPreservesTraceContext() => Prop.ForAll(Generators.ActivityContextArbitrary(), (activityContext) =>
    {
        try
        {
            var propagator = new B3Propagator();
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);

            propagator.Inject(new PropagationContext(activityContext, default), carrier, FuzzTestHelpers.Setter);

            var extracted = propagator.Extract(default, carrier, FuzzTestHelpers.Getter);

            return
                carrier.TryGetValue(TraceIdHeader, out var traceId) &&
                carrier.TryGetValue(SpanIdHeader, out var spanId) &&
                traceId.Length == 32 &&
                spanId.Length == 16 &&
                (!carrier.TryGetValue(SampledHeader, out var sampled) || sampled == "1") &&
                HaveEquivalentB3State(activityContext, extracted.ActivityContext);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property SingleHeaderInjectExtractRoundTripPreservesTraceContext() => Prop.ForAll(Generators.ActivityContextArbitrary(), (activityContext) =>
    {
        try
        {
            var propagator = new B3Propagator(singleHeader: true);
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);

            propagator.Inject(new PropagationContext(activityContext, default), carrier, FuzzTestHelpers.Setter);

            var extracted = propagator.Extract(default, carrier, FuzzTestHelpers.Getter);

            return
                carrier.TryGetValue(CombinedHeader, out var headerValue) &&
                headerValue.Split('-').Length is 2 or 3 &&
                HaveEquivalentB3State(activityContext, extracted.ActivityContext);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property MultipleHeaderExtractIsDeterministicForArbitraryHeaders() => Prop.ForAll(Generators.B3MultipleHeaderCarrierArbitrary(), (carrier) =>
    {
        try
        {
            var propagator = new B3Propagator();
            var original = FuzzTestHelpers.CloneCarrier(carrier);

            var first = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);
            var second = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);

            return
                FuzzTestHelpers.CarriersEqual(original, carrier) &&
                HaveEquivalentB3State(first.ActivityContext, second.ActivityContext);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property SingleHeaderExtractIsDeterministicForArbitraryHeaders() => Prop.ForAll(Generators.B3SingleHeaderCarrierArbitrary(), (carrier) =>
    {
        try
        {
            var propagator = new B3Propagator(singleHeader: true);
            var original = FuzzTestHelpers.CloneCarrier(carrier);

            var first = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);
            var second = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);

            return
                FuzzTestHelpers.CarriersEqual(original, carrier) &&
                HaveEquivalentB3State(first.ActivityContext, second.ActivityContext);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    private static bool HaveEquivalentB3State(ActivityContext expected, ActivityContext actual) =>
        expected.TraceId == actual.TraceId &&
        expected.SpanId == actual.SpanId &&
        expected.TraceFlags == actual.TraceFlags;
}
