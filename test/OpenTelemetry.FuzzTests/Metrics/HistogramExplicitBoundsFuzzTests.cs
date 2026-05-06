// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.FuzzTests;

public class HistogramExplicitBoundsFuzzTests
{
    [Property(MaxTest = 100)]
    public Property SmallBoundarySetsMatchScalarBaseline() => Prop.ForAll(
        CreateScenarioArbitrary(0, HistogramExplicitBounds.DefaultBoundaryCountForBinarySearch - 1),
        (scenario) => MatchesScalarBaseline(scenario.Item1, scenario.Item2));

    [Property(MaxTest = 100)]
    public Property LargeBoundarySetsMatchScalarBaseline() => Prop.ForAll(
        CreateScenarioArbitrary(HistogramExplicitBounds.DefaultBoundaryCountForBinarySearch, 256),
        (scenario) => MatchesScalarBaseline(scenario.Item1, scenario.Item2));

    private static Arbitrary<Tuple<double[], double[]>> CreateScenarioArbitrary(int minBoundaryCount, int maxBoundaryCount)
    {
        var gen =
            from boundaryCount in Gen.Choose(minBoundaryCount, maxBoundaryCount)
            from start in Gen.Choose(-1000, 1000)
            from scale in Gen.Choose(1, 16)
            from increments in Gen.ArrayOf(Gen.Choose(1, 1000), boundaryCount)
            from includeNegativeInfinity in Gen.Elements(true, false)
            from includePositiveInfinity in Gen.Elements(true, false)
            select CreateScenario(
                boundaryCount,
                start,
                scale,
                increments,
                includeNegativeInfinity,
                includePositiveInfinity);

        return gen.ToArbitrary();
    }

    private static bool MatchesScalarBaseline(double[] rawBoundaries, double[] testValues)
    {
        var histogramExplicitBounds = new HistogramExplicitBounds(rawBoundaries);
        var expectedBounds = CleanBoundaries(rawBoundaries);

        if (!expectedBounds.SequenceEqual(histogramExplicitBounds.Bounds))
        {
            return false;
        }

        foreach (var value in testValues)
        {
            if (histogramExplicitBounds.FindBucketIndex(value) != FindBucketIndexScalar(expectedBounds, value))
            {
                return false;
            }
        }

        return true;
    }

    private static Tuple<double[], double[]> CreateScenario(
        int boundaryCount,
        int start,
        int scale,
        int[] increments,
        bool includeNegativeInfinity,
        bool includePositiveInfinity)
    {
        var finiteBoundaries = new double[boundaryCount];
        double current = start;

        for (var i = 0; i < boundaryCount; i++)
        {
            current += increments[i] / (double)scale;
            finiteBoundaries[i] = current;
        }

        var rawBoundaryCount = boundaryCount
            + (includeNegativeInfinity ? 1 : 0)
            + (includePositiveInfinity ? 1 : 0);

        var rawBoundaries = new double[rawBoundaryCount];
        var rawBoundaryIndex = 0;

        if (includeNegativeInfinity)
        {
            rawBoundaries[rawBoundaryIndex++] = double.NegativeInfinity;
        }

        Array.Copy(finiteBoundaries, 0, rawBoundaries, rawBoundaryIndex, finiteBoundaries.Length);
        rawBoundaryIndex += finiteBoundaries.Length;

        if (includePositiveInfinity)
        {
            rawBoundaries[rawBoundaryIndex] = double.PositiveInfinity;
        }

        return Tuple.Create(rawBoundaries, CreateTestValues(finiteBoundaries));
    }

    private static double[] CreateTestValues(double[] cleanedBoundaries)
    {
        var values = new List<double>(cleanedBoundaries.Length + 8)
        {
            double.NaN,
            double.NegativeInfinity,
            double.PositiveInfinity,
            0.0,
        };

        if (cleanedBoundaries.Length == 0)
        {
            values.Add(-1.0);
            values.Add(1.0);
            return [.. values];
        }

        values.Add(cleanedBoundaries[0] - 1.0);
        values.Add(cleanedBoundaries[0]);

        var step = Math.Max(1, cleanedBoundaries.Length / 16);

        for (var i = step; i < cleanedBoundaries.Length; i += step)
        {
            values.Add(cleanedBoundaries[i]);
            values.Add(cleanedBoundaries[i - 1] + ((cleanedBoundaries[i] - cleanedBoundaries[i - 1]) / 2));
        }

        values.Add(cleanedBoundaries[cleanedBoundaries.Length - 1]);
        values.Add(cleanedBoundaries[cleanedBoundaries.Length - 1] + 1.0);

        return [.. values];
    }

    private static double[] CleanBoundaries(double[] rawBoundaries)
    {
        for (var i = 0; i < rawBoundaries.Length; i++)
        {
            if (double.IsNegativeInfinity(rawBoundaries[i]) || double.IsPositiveInfinity(rawBoundaries[i]))
            {
                return [.. rawBoundaries.Where(b => !double.IsNegativeInfinity(b) && !double.IsPositiveInfinity(b))];
            }
        }

        return rawBoundaries;
    }

    private static int FindBucketIndexScalar(double[] bounds, double value)
    {
        if (double.IsNaN(value))
        {
            return bounds.Length;
        }

        for (var i = 0; i < bounds.Length; i++)
        {
            if (value <= bounds[i])
            {
                return i;
            }
        }

        return bounds.Length;
    }
}
