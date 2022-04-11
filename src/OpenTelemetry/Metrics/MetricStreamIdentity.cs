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

using System;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    internal readonly struct MetricStreamIdentity : IEquatable<MetricStreamIdentity>
    {
        private static readonly StringArrayEqualityComparer StringArrayComparer = new StringArrayEqualityComparer();
        private readonly int hashCode;

        public MetricStreamIdentity(Meter meter, string instrumentName, string unit, string description, Type instrumentType, string[] tagKeys, double[] histogramBucketBounds)
        {
            this.MeterName = meter.Name;
            this.MeterVersion = meter.Version ?? string.Empty;
            this.InstrumentName = instrumentName;
            this.Unit = unit ?? string.Empty;
            this.Description = description ?? string.Empty;
            this.InstrumentType = instrumentType;

            if (tagKeys != null && tagKeys.Length > 0)
            {
                this.TagKeys = new string[tagKeys.Length];
                tagKeys.CopyTo(this.TagKeys, 0);
            }
            else
            {
                this.TagKeys = null;
            }

            if (histogramBucketBounds != null && histogramBucketBounds.Length > 0)
            {
                this.HistogramBucketBounds = new double[histogramBucketBounds.Length];
                histogramBucketBounds.CopyTo(this.HistogramBucketBounds, 0);
            }
            else
            {
                this.HistogramBucketBounds = null;
            }

            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + this.InstrumentType.GetHashCode();
                hash = (hash * 31) + this.MeterName.GetHashCode();
                hash = (hash * 31) + this.MeterVersion.GetHashCode();
                hash = (hash * 31) + this.InstrumentName.GetHashCode();
                hash = this.Unit == null ? hash : (hash * 31) + this.Unit.GetHashCode();
                hash = this.Description == null ? hash : (hash * 31) + this.Description.GetHashCode();
                hash = this.TagKeys == null ? hash : (hash * 31) + StringArrayComparer.GetHashCode(this.TagKeys);
                if (this.HistogramBucketBounds != null)
                {
                    var len = this.HistogramBucketBounds.Length;
                    for (var i = 0; i < len; ++i)
                    {
                        hash = (hash * 31) + this.HistogramBucketBounds[i].GetHashCode();
                    }
                }

                this.hashCode = hash;
            }
        }

        public string MeterName { get; }

        public string MeterVersion { get; }

        public string InstrumentName { get; }

        public string Unit { get; }

        public string Description { get; }

        public Type InstrumentType { get; }

        public string[] TagKeys { get; }

        public double[] HistogramBucketBounds { get; }

        public static bool operator ==(MetricStreamIdentity metricIdentity1, MetricStreamIdentity metricIdentity2) => metricIdentity1.Equals(metricIdentity2);

        public static bool operator !=(MetricStreamIdentity metricIdentity1, MetricStreamIdentity metricIdentity2) => !metricIdentity1.Equals(metricIdentity2);

        public readonly override bool Equals(object obj)
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
                && StringArrayComparer.Equals(this.TagKeys, other.TagKeys)
                && HistogramBoundsEqual(this.HistogramBucketBounds, other.HistogramBucketBounds);
        }

        public readonly override int GetHashCode() => this.hashCode;

        private static bool HistogramBoundsEqual(double[] bounds1, double[] bounds2)
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
}
