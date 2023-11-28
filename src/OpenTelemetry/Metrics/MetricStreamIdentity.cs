// <copyright file="MetricStreamIdentity.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
        this.MeterTags = instrument.Meter.Tags ?? Enumerable.Empty<KeyValuePair<string, object?>>();
        this.InstrumentName = metricStreamConfiguration?.Name ?? instrument.Name;
        this.Unit = instrument.Unit ?? string.Empty;
        this.Description = metricStreamConfiguration?.Description ?? instrument.Description ?? string.Empty;
        this.InstrumentType = instrument.GetType();
        this.ViewId = metricStreamConfiguration?.ViewId;
        this.MetricStreamName = $"{this.MeterName}.{this.MeterVersion}.{this.InstrumentName}";
        this.TagKeys = metricStreamConfiguration?.CopiedTagKeys;
        this.HistogramBucketBounds = (metricStreamConfiguration as ExplicitBucketHistogramConfiguration)?.CopiedBoundaries;
        this.ExponentialHistogramMaxSize = (metricStreamConfiguration as Base2ExponentialBucketHistogramConfiguration)?.MaxSize ?? 0;
        this.ExponentialHistogramMaxScale = (metricStreamConfiguration as Base2ExponentialBucketHistogramConfiguration)?.MaxScale ?? 0;
        this.HistogramRecordMinMax = (metricStreamConfiguration as HistogramConfiguration)?.RecordMinMax ?? true;

#if NET6_0_OR_GREATER
        HashCode hashCode = default;
        hashCode.Add(this.InstrumentType);
        hashCode.Add(this.MeterName);
        hashCode.Add(this.MeterVersion);
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
            for (var i = 0; i < this.HistogramBucketBounds.Length; ++i)
            {
                hashCode.Add(this.HistogramBucketBounds[i]);
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

            // MeterTags is not part of identity, so not included here.
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

    public IEnumerable<KeyValuePair<string, object?>> MeterTags { get; }

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
            && this.HistogramRecordMinMax == other.HistogramRecordMinMax
            && this.ExponentialHistogramMaxSize == other.ExponentialHistogramMaxSize
            && this.ExponentialHistogramMaxScale == other.ExponentialHistogramMaxScale
            && StringArrayComparer.Equals(this.TagKeys, other.TagKeys)
            && HistogramBoundsEqual(this.HistogramBucketBounds, other.HistogramBucketBounds);
    }

    public override readonly int GetHashCode() => this.hashCode;

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
