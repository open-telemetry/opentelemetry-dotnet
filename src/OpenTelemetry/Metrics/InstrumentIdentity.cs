// <copyright file="InstrumentIdentity.cs" company="OpenTelemetry Authors">
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
    internal readonly struct InstrumentIdentity : IEquatable<InstrumentIdentity>
    {
        private readonly int hashCode;

        public InstrumentIdentity(Meter meter, string instrumentName, string unit, string description, Type instrumentType)
        {
            this.MeterName = meter.Name;
            this.MeterVersion = meter.Version ?? string.Empty;
            this.InstrumentName = instrumentName;
            this.Unit = unit ?? string.Empty;
            this.Description = description ?? string.Empty;
            this.InstrumentType = instrumentType;

            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + this.InstrumentType.GetHashCode();
                hash = (hash * 31) + this.MeterName.GetHashCode();
                hash = (hash * 31) + this.MeterVersion.GetHashCode();
                hash = (hash * 31) + this.InstrumentName.GetHashCode();
                hash = this.Unit == null ? hash : (hash * 31) + this.Unit.GetHashCode();
                hash = this.Description == null ? hash : (hash * 31) + this.Description.GetHashCode();
                this.hashCode = hash;
            }
        }

        public readonly string MeterName { get; }

        public readonly string MeterVersion { get; }

        public readonly string InstrumentName { get; }

        public readonly string Unit { get; }

        public readonly string Description { get; }

        public readonly Type InstrumentType { get; }

        public static bool operator ==(InstrumentIdentity metricIdentity1, InstrumentIdentity metricIdentity2) => metricIdentity1.Equals(metricIdentity2);

        public static bool operator !=(InstrumentIdentity metricIdentity1, InstrumentIdentity metricIdentity2) => !metricIdentity1.Equals(metricIdentity2);

        public readonly override bool Equals(object obj)
        {
            return obj is InstrumentIdentity other && this.Equals(other);
        }

        public bool Equals(InstrumentIdentity other)
        {
            return this.InstrumentType == other.InstrumentType
                && this.MeterName == other.MeterName
                && this.MeterVersion == other.MeterVersion
                && this.InstrumentName == other.InstrumentName
                && this.Unit == other.Unit
                && this.Description == other.Description;
        }

        public readonly override int GetHashCode() => this.hashCode;
    }
}
