// <copyright file="View.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Tags;

    public sealed class View : IView
    {
        internal View(IViewName name, string description, IMeasure measure, IAggregation aggregation, IReadOnlyList<TagKey> columns)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Description = description ?? throw new ArgumentNullException(nameof(description));
            this.Measure = measure ?? throw new ArgumentNullException(nameof(measure));
            this.Aggregation = aggregation ?? throw new ArgumentNullException(nameof(aggregation));
            this.Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        }

        public IViewName Name { get; }

        public string Description { get; }

        public IMeasure Measure { get; }

        public IAggregation Aggregation { get; }

        public IReadOnlyList<TagKey> Columns { get; }

        public static IView Create(IViewName name, string description, IMeasure measure, IAggregation aggregation, IReadOnlyList<TagKey> columns)
        {
            var set = new HashSet<TagKey>(columns);
            if (set.Count != columns.Count)
            {
                throw new ArgumentException("Columns have duplicate.");
            }

            return new View(
                name,
                description,
                measure,
                aggregation,
                new List<TagKey>(columns).AsReadOnly());
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "View{"
                + "name=" + this.Name + ", "
                + "description=" + this.Description + ", "
                + "measure=" + this.Measure + ", "
                + "aggregation=" + this.Aggregation + ", "
                + "columns=" + this.Columns + ", "
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is View that)
            {
                return this.Name.Equals(that.Name)
                     && this.Description.Equals(that.Description)
                     && this.Measure.Equals(that.Measure)
                     && this.Aggregation.Equals(that.Aggregation)
                     && this.Columns.SequenceEqual(that.Columns);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Name.GetHashCode();
            h *= 1000003;
            h ^= this.Description.GetHashCode();
            h *= 1000003;
            h ^= this.Measure.GetHashCode();
            h *= 1000003;
            h ^= this.Aggregation.GetHashCode();
            h *= 1000003;
            h ^= this.Columns.GetHashCode();
            h *= 1000003;
            return h;
        }
    }
}
