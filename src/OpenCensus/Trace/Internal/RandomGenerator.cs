// <copyright file="RandomGenerator.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Internal
{
    using System;

    internal class RandomGenerator : IRandomGenerator
    {
        private static readonly Random Global = new Random();

        [ThreadStatic]
        private static Random local;

        private readonly int seed;
        private readonly bool sameSeed;

        internal RandomGenerator()
        {
            this.sameSeed = false;
        }

        /// <summary>
        /// This constructur uses the same seed for all the thread static random objects.
        /// You might get the same values if a random is accessed from different threads.
        /// Use only for unit tests...
        /// </summary>
        internal RandomGenerator(int seed)
        {
            this.sameSeed = true;
            this.seed = seed;
        }

        public void NextBytes(byte[] bytes)
        {
            if (local == null)
            {
                local = new Random(this.sameSeed ? this.seed : Global.Next());
            }

            local.NextBytes(bytes);
        }
    }
}
