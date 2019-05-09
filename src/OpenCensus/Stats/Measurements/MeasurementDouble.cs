// <copyright file="MeasurementDouble.cs" company="OpenCensus Authors">
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
    using OpenCensus.Utils;

    public sealed class MeasurementDouble : Measurement, IMeasurementDouble
    {
        internal MeasurementDouble(IMeasureDouble measure, double value)
        {
            this.Measure = measure ?? throw new ArgumentNullException(nameof(measure));
            this.Value = value;
        }

        public override IMeasure Measure { get; }

        public double Value { get; }

        public static IMeasurementDouble Create(IMeasureDouble measure, double value)
        {
            return new MeasurementDouble(measure, value);
        }

        public override T Match<T>(Func<IMeasurementDouble, T> p0, Func<IMeasurementLong, T> p1, Func<IMeasurement, T> defaultFunction)
        {
            return p0.Invoke(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "MeasurementDouble{"
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

            if (o is MeasurementDouble that)
            {
                return this.Measure.Equals(that.Measure)
                     && (DoubleUtil.ToInt64(this.Value) == DoubleUtil.ToInt64(that.Value));
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
            h ^= (DoubleUtil.ToInt64(this.Value) >> 32) ^ DoubleUtil.ToInt64(this.Value);
            return (int)h;
        }
    }
}
