// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.FuzzTests;

public class YamlScalarConverterFuzzTests
{
    private const int MaxTests = 500;

    private static readonly Arbitrary<string> ScalarStringArbitrary = Gen.Sized(size =>
        Gen.ArrayOf(
            Gen.Choose(0, 127).Select(c => (char)c),
            Math.Min(size + 1, 256))
        .Select(chars => new string(chars))).ToArbitrary();

    [Property(MaxTest = MaxTests)]
    public Property ResolveAndConvertNeverThrowsForArbitraryPlainScalar() =>
        Prop.ForAll(
            ScalarStringArbitrary,
            value =>
            {
                var node = new YamlScalarNode(value) { Style = ScalarStyle.Plain };
                var resolved = YamlScalarResolver.Resolve(node, value);
                _ = YamlScalarConverter.Convert(resolved);
            });
}
