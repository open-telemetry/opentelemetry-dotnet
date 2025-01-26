// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

internal readonly struct MetricStreamIdentity : IEquatable<MetricStreamIdentity>
{
    private static readonly StringArrayEqualityComparer StringArrayComparer = new();
    private readonly int hashCode;

    public MetricStreamIdentity(Instrument instrument, MetricStreamConfiguration? metricStreamConfiguration)
    {
        this.MeterName = instrument.Meter.Name;
        this.MeterVersion = instrument.Meter.Version ?? string.Empty;
        this.MeterTags = instrument.Meter.Tags != null ? new Tags(instrument.Meter.Tags.ToArray()) : null;
        this.InstrumentName = metricStreamConfiguration?.Name ?? instrument.Name;
        this.Unit = instrument.Unit ?? string.Empty;
        this.Description = metricStreamConfiguration?.Description ?? instrument.Description ?? string.Empty;
        this.InstrumentType = instrument.GetType();
        this.ViewId = metricStreamConfiguration?.ViewId;
        this.MetricStreamName = $"{this.MeterName}.{this.MeterVersion}.{this.InstrumentName}";
        this.TagKeys = metricStreamConfiguration?.CopiedTagKeys;
        this.HistogramBucketBounds = GetExplicitBucketHistogramBounds(instrument, metricStreamConfiguration);
        this.ExponentialHistogramMaxSize = (metricStreamConfiguration as Base2ExponentialBucketHistogramConfiguration)?.MaxSize ?? 0;
        this.ExponentialHistogramMaxScale = (metricStreamConfiguration as Base2ExponentialBucketHistogramConfiguration)?.MaxScale ?? 0;
        this.HistogramRecordMinMax = (metricStreamConfiguration as HistogramConfiguration)?.RecordMinMax ?? true;

#if NET
        HashCode hashCode = default;
        hashCode.Add(this.InstrumentType);
        hashCode.Add(this.MeterName);
        hashCode.Add(this.MeterVersion);
        hashCode.Add(this.MeterTags);
        hashCode.Add(this.InstrumentName);
        hashCode.Add(this.HistogramRecordMinMax);
        hashCode.Add(this.Unit);
        hashCode.Add(this.Description);
        hashCode.Add(this.ViewId);

        // Note: The this.TagKeys! here is strange but it is fine for the value
        // to be null. HashCode.Add is coded to handle the value being null. We
        // are essentially suppressing a false positive due to an issue/quirk
        // with the annotations. See:
        // https://github.com/dotnet/runtime/pull/91905.
        hashCode.Add(this.TagKeys!, StringArrayComparer);

        hashCode.Add(this.ExponentialHistogramMaxSize);
        hashCode.Add(this.ExponentialHistogramMaxScale);
        if (this.HistogramBucketBounds != null)
        {
            foreach (var bound in this.HistogramBucketBounds)
            {
                hashCode.Add(bound);
            }
        }

        var hash = hashCode.ToHashCode();
#else
        var hash = 17;
        unchecked
        {
            hash = (hash * 31) + this.InstrumentType.GetHashCode();
            hash = (hash * 31) + this.MeterName.GetHashCode();
            hash = (hash * 31) + this.MeterVersion.GetHashCode();
            hash = (hash * 31) + this.MeterTags?.GetHashCode() ?? 0;
            hash = (hash * 31) + this.InstrumentName.GetHashCode();
            hash = (hash * 31) + this.HistogramRecordMinMax.GetHashCode();
            hash = (hash * 31) + this.ExponentialHistogramMaxSize.GetHashCode();
            hash = (hash * 31) + this.ExponentialHistogramMaxScale.GetHashCode();
            hash = (hash * 31) + this.Unit.GetHashCode();
            hash = (hash * 31) + this.Description.GetHashCode();
            hash = (hash * 31) + (this.ViewId ?? 0);
            hash = (hash * 31) + (this.TagKeys != null ? StringArrayComparer.GetHashCode(this.TagKeys) : 0);
            if (this.HistogramBucketBounds != null)
            {
                var len = this.HistogramBucketBounds.Length;
                for (var i = 0; i < len; ++i)
                {
                    hash = (hash * 31) + this.HistogramBucketBounds[i].GetHashCode();
                }
            }
        }

#endif
        this.hashCode = hash;
    }

    public string MeterName { get; }

    public string MeterVersion { get; }

    public Tags? MeterTags { get; }

    public string InstrumentName { get; }

    public string Unit { get; }

    public string Description { get; }

    public Type InstrumentType { get; }

    public int? ViewId { get; }

    public string MetricStreamName { get; }

    public string[]? TagKeys { get; }

    public double[]? HistogramBucketBounds { get; }

    public int ExponentialHistogramMaxSize { get; }

    public int ExponentialHistogramMaxScale { get; }

    public bool HistogramRecordMinMax { get; }

    public bool IsHistogram =>
        this.InstrumentType == typeof(Histogram<long>)
        || this.InstrumentType == typeof(Histogram<int>)
        || this.InstrumentType == typeof(Histogram<short>)
        || this.InstrumentType == typeof(Histogram<byte>)
        || this.InstrumentType == typeof(Histogram<float>)
        || this.InstrumentType == typeof(Histogram<double>);

    public static bool operator ==(MetricStreamIdentity metricIdentity1, MetricStreamIdentity metricIdentity2) => metricIdentity1.Equals(metricIdentity2);

    public static bool operator !=(MetricStreamIdentity metricIdentity1, MetricStreamIdentity metricIdentity2) => !metricIdentity1.Equals(metricIdentity2);

    public override readonly bool Equals(object? obj)
    {
        return obj is MetricStreamIdentity other && this.Equals(other);
    }

    public bool Equals(MetricStreamIdentity other)
    {
        return this.InstrumentType == other.InstrumentType
            && this.MeterName == other.MeterName
            && this.MeterVersion == other.MeterVersion
            && this.InstrumentName == other.InstrumentName
            && this.Unit == other.Unit
            && this.Description == other.Description
            && this.ViewId == other.ViewId
            && this.MeterTags == other.MeterTags
            && this.HistogramRecordMinMax == other.HistogramRecordMinMax
            && this.ExponentialHistogramMaxSize == other.ExponentialHistogramMaxSize
            && this.ExponentialHistogramMaxScale == other.ExponentialHistogramMaxScale
            && StringArrayComparer.Equals(this.TagKeys, other.TagKeys)
            && HistogramBoundsEqual(this.HistogramBucketBounds, other.HistogramBucketBounds);
    }

    public override readonly int GetHashCode() => this.hashCode;

    private static double[]? GetExplicitBucketHistogramBounds(Instrument instrument, MetricStreamConfiguration? metricStreamConfiguration)
    {
        if (metricStreamConfiguration is ExplicitBucketHistogramConfiguration explicitBucketHistogramConfiguration
            && explicitBucketHistogramConfiguration.CopiedBoundaries != null)
        {
            return explicitBucketHistogramConfiguration.CopiedBoundaries;
        }

        return instrument switch
        {
            Histogram<long> longHistogram => GetExplicitBucketHistogramBoundsFromAdvice(longHistogram),
            Histogram<int> intHistogram => GetExplicitBucketHistogramBoundsFromAdvice(intHistogram),
            Histogram<short> shortHistogram => GetExplicitBucketHistogramBoundsFromAdvice(shortHistogram),
            Histogram<byte> byteHistogram => GetExplicitBucketHistogramBoundsFromAdvice(byteHistogram),
            Histogram<float> floatHistogram => GetExplicitBucketHistogramBoundsFromAdvice(floatHistogram),
            Histogram<double> doubleHistogram => GetExplicitBucketHistogramBoundsFromAdvice(doubleHistogram),
            _ => null,
        };
    }

    private static double[]? GetExplicitBucketHistogramBoundsFromAdvice<T>(Histogram<T> histogram)
        where T : struct
    {
        var adviceExplicitBucketBoundaries = histogram.Advice?.HistogramBucketBoundaries;
        if (adviceExplicitBucketBoundaries == null)
        {
            return null;
        }

        if (typeof(T) == typeof(double))
        {
            return ((IReadOnlyList<double>)adviceExplicitBucketBoundaries).ToArray();
        }
        else
        {
            double[] explicitBucketBoundaries = new double[adviceExplicitBucketBoundaries.Count];

            for (int i = 0; i < adviceExplicitBucketBoundaries.Count; i++)
            {
                explicitBucketBoundaries[i] = Convert.ToDouble(adviceExplicitBucketBoundaries[i]);
            }

            return explicitBucketBoundaries;
        }
    }

    private static bool HistogramBoundsEqual(double[]? bounds1, double[]? bounds2)
    {
        if (ReferenceEquals(bounds1, bounds2))
        {
            return true;
        }

        if (ReferenceEquals(bounds1, null) || ReferenceEquals(bounds2, null))
        {
            return false;
        }

        var len1 = bounds1.Length;

        if (len1 != bounds2.Length)
        {
            return false;
        }

        for (int i = 0; i < len1; i++)
        {
            if (!bounds1[i].Equals(bounds2[i]))
            {
                return false;
            }
        }

        return true;
    }
}
