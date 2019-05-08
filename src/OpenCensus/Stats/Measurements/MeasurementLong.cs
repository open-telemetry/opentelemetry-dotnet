// <copyright file="MeasurementLong.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Stats.Measurements
{
    using System;
    using OpenCensus.Stats.Measures;

    public sealed class MeasurementLong : Measurement, IMeasurementLong
    {
        private MeasurementLong(IMeasureLong measure, long value)
        {
            this.Measure = measure ?? throw new ArgumentNullException(nameof(measure));
            this.Value = value;
        }

        public override IMeasure Measure { get; }

        public long Value { get; }

        public static IMeasurementLong Create(IMeasureLong measure, long value)
        {
            return new MeasurementLong(measure, value);
        }

        public override T Match<T>(Func<IMeasurementDouble, T> p0, Func<IMeasurementLong, T> p1, Func<IMeasurement, T> defaultFunction)
        {
            return p1.Invoke(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "MeasurementLong{"
                + "measure=" + this.Measure + ", "
                + "value=" + this.Value
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is MeasurementLong that)
            {
                return this.Measure.Equals(that.Measure)
                     && (this.Value == that.Value);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            long h = 1;
            h *= 1000003;
            h ^= this.Measure.GetHashCode();
            h *= 1000003;
            h ^= (this.Value >> 32) ^ this.Value;
            return (int)h;
        }
    }
}
