// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores configuration for a histogram metric stream with explicit bucket boundaries.
/// </summary>
public class ExplicitBucketHistogramConfiguration : HistogramConfiguration
{
    internal const int MaxBoundaryCount = 10_000_000;

#pragma warning disable CA1819 // Properties should not return arrays
    /// <summary>
    /// Gets or sets the optional boundaries of the histogram metric stream.
    /// </summary>
    /// <remarks>
    /// Requirements:
    /// <list type="bullet">
    /// <item>The array must be in ascending order with distinct
    /// values.</item>
    /// <item>An empty array would result in no histogram buckets being
    /// calculated.</item>
    /// <item>A null value would result in default bucket boundaries being
    /// used.</item>
    /// <item>The array must not contain more than 10,000,000 values.</item>
    /// </list>
    /// Note: A copy is made of the provided array.
    /// </remarks>
    public double[]? Boundaries
#pragma warning restore CA1819 // Properties should not return arrays
    {
        get => this.CopiedBoundaries?.ToArray();

        set
        {
            if (value != null)
            {
                ThrowIfBoundaryCountExceedsLimit(value.Length, nameof(value));

                if (!IsSortedAndDistinct(value))
                {
                    throw new ArgumentException($"Histogram boundaries are invalid. Histogram boundaries must be in ascending order with distinct values.", nameof(value));
                }

                this.CopiedBoundaries = [.. value];
            }
            else
            {
                this.CopiedBoundaries = null;
            }
        }
    }

    internal double[]? CopiedBoundaries { get; private set; }

    internal static void ThrowIfBoundaryCountExceedsLimit(int boundaryCount, string? paramName)
    {
        if (boundaryCount > MaxBoundaryCount)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                boundaryCount,
                $"Histogram boundaries are invalid. Maximum supported boundary count is {MaxBoundaryCount}.");
        }
    }

    private static bool IsSortedAndDistinct(double[] values)
    {
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] <= values[i - 1])
            {
                return false;
            }
        }

        return true;
    }
}
