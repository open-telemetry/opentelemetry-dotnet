// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Api.FuzzTests;

public class EnvironmentVariableCarrierFuzzTests
{
    private const int MaxTests = 200;

    [Property(MaxTest = MaxTests)]
    public Property NormalizeKeyProducesValidEnvironmentVariableNames() => Prop.ForAll(Generators.EnvironmentKeyArbitrary(), key =>
    {
        try
        {
            var normalizedKey = EnvironmentVariableCarrier.NormalizeKey(key);

            return
                normalizedKey.All(IsValidEnvironmentCharacter) &&
                (normalizedKey.Length == 0 || !IsAsciiDigit(normalizedKey[0]));
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property TraceContextRoundTripWorksWithEnvironmentVariableCarrier() => Prop.ForAll(Generators.ActivityContextArbitrary(), activityContext =>
    {
        try
        {
            var carrier = new Dictionary<string, string?>(StringComparer.Ordinal);
            var propagator = new TraceContextPropagator();

            propagator.Inject(new PropagationContext(activityContext, default), carrier, EnvironmentVariableCarrier.Set);

            var extracted = propagator.Extract(default, EnvironmentVariableCarrier.Capture(carrier), EnvironmentVariableCarrier.Get);

            return AreEquivalent(activityContext, extracted.ActivityContext);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property BaggageRoundTripWorksWithEnvironmentVariableCarrier() => Prop.ForAll(Generators.SafeBaggageDictionaryArbitrary(), baggageItems =>
    {
        try
        {
            var carrier = new Dictionary<string, string?>(StringComparer.Ordinal);
            var propagator = new BaggagePropagator();

            propagator.Inject(new PropagationContext(default, Baggage.Create(baggageItems)), carrier, EnvironmentVariableCarrier.Set);

            var extracted = propagator.Extract(default, EnvironmentVariableCarrier.Capture(carrier), EnvironmentVariableCarrier.Get);

            return DictionariesEqual(baggageItems, extracted.Baggage.GetBaggage());
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property CaptureRoundTripPreservesOpaqueValues() => Prop.ForAll(Generators.EnvironmentCarrierArbitrary(), carrier =>
    {
        try
        {
            var snapshot = EnvironmentVariableCarrier.Capture(carrier);
            var reinjected = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var pair in snapshot)
            {
                var value = EnvironmentVariableCarrier.Get(snapshot, pair.Key);
                if (value is not null)
                {
                    EnvironmentVariableCarrier.Set(reinjected, pair.Key, value.Single());
                }
            }

            return snapshot.Count == reinjected.Count &&
                snapshot.All(pair => reinjected.TryGetValue(pair.Key, out var value) && value == pair.Value);
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

    private static bool DictionariesEqual(Dictionary<string, string> expected, IReadOnlyDictionary<string, string> actual) =>
        expected.Count == actual.Count &&
        expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static bool IsValidEnvironmentCharacter(char value) =>
        (value >= 'A' && value <= 'Z') ||
        (value >= '0' && value <= '9') ||
        value == '_';

    private static bool IsAsciiDigit(char value) => value >= '0' && value <= '9';
}
