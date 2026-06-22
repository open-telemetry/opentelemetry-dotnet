// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// DeclarativeConfigurationException carries the OTEL1006 experimental attribute.
// Suppress once here rather than at every catch site.
#pragma warning disable OTEL1006

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace OpenTelemetry.Configuration.Declarative.FuzzTests;

public class EnvironmentSubstitutionFuzzTests
{
    private const int MaxTests = 500;

    // Characters relevant to substitution syntax, weighted higher in generation.
    private static readonly Arbitrary<string> SubstitutionStringArbitrary = Gen.Sized(size =>
        Gen.ArrayOf(
            Gen.OneOf(
                Gen.Elements("${}:-_ABCDabc012 ".ToCharArray()),
                Gen.Choose(0, 127).Select(c => (char)c)),
            Math.Min(size + 1, 256))
        .Select(chars => new string(chars))).ToArbitrary();

    [Property(MaxTest = MaxTests)]
    public Property SubstituteNeverThrowsUnexpectedException() =>
        Prop.ForAll(
            SubstitutionStringArbitrary,
            value =>
            {
                try
                {
                    _ = EnvironmentSubstitution.Substitute(value, _ => null);
                }
                catch (DeclarativeConfigurationException)
                { }
            });

    [Property(MaxTest = MaxTests)]
    public Property SubstituteIsDeterministicForArbitraryInputs() =>
        Prop.ForAll(
            SubstitutionStringArbitrary,
            value =>
            {
                try
                {
                    var first = EnvironmentSubstitution.Substitute(value, _ => null);
                    var second = EnvironmentSubstitution.Substitute(value, _ => null);
                    return first == second;
                }
                catch (DeclarativeConfigurationException)
                {
                    return true;
                }
            });
}
