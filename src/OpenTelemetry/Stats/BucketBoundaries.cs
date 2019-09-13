// <copyright file="BucketBoundaries.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Utils;

    public sealed class BucketBoundaries : IBucketBoundaries
    {
        private BucketBoundaries(IReadOnlyList<double> boundaries)
        {
            this.Boundaries = boundaries;
        }

        public IReadOnlyList<double> Boundaries { get; }

        public static IBucketBoundaries Create(IEnumerable<double> bucketBoundaries)
        {
            if (bucketBoundaries == null)
            {
                throw new ArgumentNullException(nameof(bucketBoundaries));
            }

            var bucketBoundariesCopy = new List<double>(bucketBoundaries);

            if (bucketBoundariesCopy.Count > 1)
            {
                var lower = bucketBoundariesCopy[0];
                for (var i = 1; i < bucketBoundariesCopy.Count; i++)
                {
                    var next = bucketBoundariesCopy[i];
                    if (!(lower < next))
                    {
                        throw new ArgumentOutOfRangeException(nameof(bucketBoundaries), "Bucket boundaries not sorted.");
                    }

                    lower = next;
                }
            }

            return new BucketBoundaries(bucketBoundariesCopy.AsReadOnly());
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return nameof(BucketBoundaries)
                + "{"
                + nameof(this.Boundaries) + "=" + string.Join(", ", this.Boundaries)
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is BucketBoundaries that)
            {
                return this.Boundaries.SequenceEqual(that.Boundaries);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Boundaries.GetHashCode();
            return h;
        }
    }
}
