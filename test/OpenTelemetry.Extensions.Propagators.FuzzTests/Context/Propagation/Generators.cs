// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.Extensions.Propagators.FuzzTests;

internal static class Generators
{
    public static Arbitrary<string> DelimiterFloodArbitrary(char delimiter, int minLength = 1024, int maxLength = 50_000)
    {
        var gen =
            from length in Gen.Choose(minLength, maxLength)
            select new string(delimiter, length);

        return gen.ToArbitrary();
    }

    public static Arbitrary<string> JaegerManyComponentHeaderArbitrary(int minParts = 1024, int maxParts = 50_000)
    {
        var gen =
            from delimiter in Gen.Elements(":", "%3A")
            from count in Gen.Choose(minParts, maxParts)
            select string.Join(delimiter, Enumerable.Repeat("part", count));

        return gen.ToArbitrary();
    }
}
