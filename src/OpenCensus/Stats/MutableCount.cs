// <copyright file="MutableCount.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Stats
{
    using System;

    internal sealed class MutableCount : MutableAggregation
    {
        internal MutableCount()
        {
        }

        internal long Count { get; private set; } = 0;

        internal static MutableCount Create()
        {
            return new MutableCount();
        }

        internal override void Add(double value)
        {
            this.Count++;
        }

        internal override void Combine(MutableAggregation other, double fraction)
        {
            if (!(other is MutableCount mutable))
            {
                throw new ArgumentException("MutableCount expected.");
            }

            var result = fraction * mutable.Count;
            long rounded = (long)Math.Round(result);
            this.Count += rounded;
        }

        internal override T Match<T>(Func<MutableSum, T> p0, Func<MutableCount, T> p1, Func<MutableMean, T> p2, Func<MutableDistribution, T> p3, Func<MutableLastValue, T> p4)
        {
            return p1.Invoke(this);
        }
    }
}
