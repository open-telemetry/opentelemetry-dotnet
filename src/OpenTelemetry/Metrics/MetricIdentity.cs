// <copyright file="MetricIdentity.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal readonly struct MetricIdentity : IEquatable<MetricIdentity>
    {
        public MetricIdentity(string meterName, string instrumentName, string unit, string description, Type instrumentType)
        {
            this.MeterName = meterName;
            this.InstrumentName = instrumentName;
            this.Unit = unit;
            this.Descripton = description;
            this.InstrumentType = instrumentType;
        }

        public readonly string MeterName { get; }

        public readonly string InstrumentName { get; }

        public readonly string Unit { get; }

        public readonly string Descripton { get; }

        public readonly Type InstrumentType { get; }

        public static bool operator ==(MetricIdentity metricIdentity1, MetricIdentity metricIdentity2) => metricIdentity1.Equals(metricIdentity2);

        public static bool operator !=(MetricIdentity metricIdentity1, MetricIdentity metricIdentity2) => !metricIdentity1.Equals(metricIdentity2);

        public readonly override bool Equals(object obj)
        {
            return obj is MetricIdentity other && this.Equals(other);
        }

        public bool Equals(MetricIdentity other)
        {
            return this.InstrumentType == other.InstrumentType
                && this.MeterName == other.MeterName
                && this.InstrumentName == other.InstrumentName
                && this.Unit == other.Unit
                && this.Descripton == other.Descripton;
        }

        public readonly override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + this.InstrumentType.GetHashCode();
                hash = (hash * 31) + this.MeterName.GetHashCode();
                hash = (hash * 31) + this.InstrumentName.GetHashCode();
                hash = this.Unit == null ? hash : (hash * 31) + this.Unit.GetHashCode();
                hash = this.Descripton == null ? hash : (hash * 31) + this.Descripton.GetHashCode();
                return hash;
            }
        }
    }
}
