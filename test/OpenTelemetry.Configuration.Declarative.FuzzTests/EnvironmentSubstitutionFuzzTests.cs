// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
                {
                }
            });

    // Escaping every '$' must round-trip exactly: after doubling, every '$' in the input belongs to
    // a '$$' pair, so no region can contain a '${' and nothing is substituted or rejected. This is
    // the core invariant of the escape-region model - if region splitting ever undercounts a run of
    // dollars, this property fails.
    [Property(MaxTest = MaxTests)]
    public Property EscapingEveryDollarRoundTrips() =>
        Prop.ForAll(
            SubstitutionStringArbitrary,
            value =>
            {
#pragma warning disable CA1307 // Specify StringComparison for clarity - the 3-arg overload is not available on all TFMs
                var escaped = value.Replace("$", "$$");
#pragma warning restore CA1307 // Specify StringComparison for clarity
                return EnvironmentSubstitution.Substitute(escaped, _ => "unused") == value;
            });

    // Resolved values are never rescanned, so a resolver that returns substitution syntax can never
    // cause a second expansion, an error, or unbounded recursion.
    [Property(MaxTest = MaxTests)]
    public Property ResolvedValuesAreNeverRescanned() =>
        Prop.ForAll(
            SubstitutionStringArbitrary,
            value =>
            {
                try
                {
                    var result = EnvironmentSubstitution.Substitute("${VAR}", _ => value);
                    return result == (value.Length == 0 ? string.Empty : value);
                }
                catch (DeclarativeConfigurationException)
                {
                    return false;
                }
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
