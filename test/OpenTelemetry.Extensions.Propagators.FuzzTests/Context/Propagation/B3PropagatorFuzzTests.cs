// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using ExtensionsB3Propagator = OpenTelemetry.Extensions.Propagators.B3Propagator;

namespace OpenTelemetry.Extensions.Propagators.FuzzTests;

[Obsolete("B3Propagator is obsolete but intentionally fuzz tested.")]
public class B3PropagatorFuzzTests
{
    private const int MaxTests = 25;
    private const string CombinedHeader = "b3";

    [Property(MaxTest = MaxTests)]
    public Property SingleHeaderDelimiterFloodReturnsDefault() => Prop.ForAll(Generators.DelimiterFloodArbitrary('-'), (headerValue) =>
    {
        try
        {
            var propagator = new ExtensionsB3Propagator(singleHeader: true);
            var carrier = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [CombinedHeader] = [headerValue],
            };
            var original = FuzzTestHelpers.CloneCarrier(carrier);

            var extracted = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);

            return extracted.Equals(default) && FuzzTestHelpers.CarriersEqual(original, carrier);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });
}
