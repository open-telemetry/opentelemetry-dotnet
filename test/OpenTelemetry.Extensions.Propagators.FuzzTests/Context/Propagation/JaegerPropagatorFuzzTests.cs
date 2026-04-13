// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using ExtensionsJaegerPropagator = OpenTelemetry.Extensions.Propagators.JaegerPropagator;

namespace OpenTelemetry.Extensions.Propagators.FuzzTests;

[Obsolete("JaegerPropagator is obsolete but intentionally fuzz tested.")]
public class JaegerPropagatorFuzzTests
{
    private const int MaxTests = 25;
    private const string JaegerHeader = "uber-trace-id";

    [Property(MaxTest = MaxTests)]
    public Property ManyComponentHeadersReturnDefault() => Prop.ForAll(Generators.JaegerManyComponentHeaderArbitrary(), (headerValue) =>
    {
        try
        {
            var propagator = new ExtensionsJaegerPropagator();
            var carrier = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [JaegerHeader] = [headerValue],
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
