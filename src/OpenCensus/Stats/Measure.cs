// <copyright file="Measure.cs" company="OpenCensus Authors">
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
    using OpenCensus.Stats.Measures;

    public abstract class Measure : IMeasure
    {
        internal const int NameMaxLength = 255;

        public abstract string Name { get; }

        public abstract string Description { get; }

        public abstract string Unit { get; }

        public abstract T Match<T>(Func<IMeasureDouble, T> p0, Func<IMeasureLong, T> p1, Func<IMeasure, T> defaultFunction);
    }
}
