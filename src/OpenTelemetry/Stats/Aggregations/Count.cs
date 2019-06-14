// <copyright file="Count.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Stats.Aggregations
{
    using System;
    using System.Diagnostics;

    [DebuggerDisplay("{ToString(),nq}")]
    public sealed class Count : Aggregation, ICount
    {
        private static readonly Count Instance = new Count();
        private static readonly string StringRepresentation = "Count{}";

        private Count()
        {
        }

        public static ICount Create()
        {
            return Instance;
        }

        public override T Match<T>(Func<ISum, T> p0, Func<ICount, T> p1, Func<IMean, T> p2, Func<IDistribution, T> p3, Func<ILastValue, T> p4, Func<IAggregation, T> p5)
        {
            return p1.Invoke(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return StringRepresentation;
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is Count)
            {
                return true;
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            return h;
        }
    }
}
