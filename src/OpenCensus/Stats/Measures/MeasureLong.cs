// <copyright file="MeasureLong.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Stats.Measures
{
    using System;
    using OpenCensus.Utils;

    public sealed class MeasureLong : Measure, IMeasureLong
    {
        internal MeasureLong(string name, string description, string unit)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Description = description ?? throw new ArgumentNullException(nameof(description));
            this.Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        }

        public override string Name { get; }

        public override string Description { get; }

        public override string Unit { get; }

        public static IMeasureLong Create(string name, string description, string unit)
        {
            if (!(StringUtil.IsPrintableString(name) && name.Length <= NameMaxLength))
            {
                throw new ArgumentOutOfRangeException(
                    "Name should be a ASCII string with a length no greater than "
                    + NameMaxLength
                    + " characters.");
            }

            return new MeasureLong(name, description, unit);
        }

        public override T Match<T>(Func<IMeasureDouble, T> p0, Func<IMeasureLong, T> p1, Func<IMeasure, T> defaultFunction)
        {
            return p1.Invoke(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "MeasureLong{"
                + "name=" + this.Name + ", "
                + "description=" + this.Description + ", "
                + "unit=" + this.Unit
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is MeasureLong that)
            {
                return this.Name.Equals(that.Name)
                     && this.Description.Equals(that.Description)
                     && this.Unit.Equals(that.Unit);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.Name.GetHashCode();
            h *= 1000003;
            h ^= this.Description.GetHashCode();
            h *= 1000003;
            h ^= this.Unit.GetHashCode();
            return h;
        }
    }
}
