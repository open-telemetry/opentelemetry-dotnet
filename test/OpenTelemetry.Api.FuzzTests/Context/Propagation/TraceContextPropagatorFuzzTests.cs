// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Api.FuzzTests;

public class TraceContextPropagatorFuzzTests
{
    private const int MaxTests = 200;
    private const string TraceParent = "traceparent";

    [Property(MaxTest = MaxTests)]
    public Property InjectExtractRoundTripPreservesValidContexts() => Prop.ForAll(Generators.ActivityContextArbitrary(), (activityContext) =>
    {
        try
        {
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);
            var propagator = new TraceContextPropagator();

            propagator.Inject(new PropagationContext(activityContext, default), carrier, FuzzTestHelpers.Setter);

            var extracted = propagator.Extract(default, carrier, FuzzTestHelpers.Getter);

            return
                carrier.TryGetValue(TraceParent, out var traceParent) &&
                traceParent is not null &&
                traceParent.Length == 55 &&
                AreEquivalent(activityContext, extracted.ActivityContext);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property ExtractIsDeterministicForArbitraryHeaders() => Prop.ForAll(Generators.TraceContextCarrierArbitrary(), (carrier) =>
    {
        try
        {
            var propagator = new TraceContextPropagator();
            var original = FuzzTestHelpers.CloneCarrier(carrier);

            var first = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);
            var second = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);

            return
                FuzzTestHelpers.CarriersEqual(original, carrier) &&
                AreEquivalent(first.ActivityContext, second.ActivityContext);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property MultipleInjectionCallsAreConsistent() => Prop.ForAll(Generators.ActivityContextArbitrary(), activityContext =>
    {
        try
        {
            var propagator = new TraceContextPropagator();
            var firstCarrier = new Dictionary<string, string>(StringComparer.Ordinal);
            var secondCarrier = new Dictionary<string, string>(StringComparer.Ordinal);
            var propagationContext = new PropagationContext(activityContext, default);

            propagator.Inject(propagationContext, firstCarrier, FuzzTestHelpers.Setter);
            propagator.Inject(propagationContext, secondCarrier, FuzzTestHelpers.Setter);

            return
                firstCarrier.Count == secondCarrier.Count &&
                firstCarrier.All(pair => secondCarrier.TryGetValue(pair.Key, out var value) && value == pair.Value);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    private static bool AreEquivalent(ActivityContext expected, ActivityContext actual) =>
        expected.TraceId == actual.TraceId &&
        expected.SpanId == actual.SpanId &&
        expected.TraceFlags == actual.TraceFlags &&
        expected.TraceState == actual.TraceState;
}
