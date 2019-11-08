﻿// <copyright file="MeasureMapBase.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context;
using OpenTelemetry.Stats.Measures;

namespace OpenTelemetry.Stats
{
    public abstract class MeasureMapBase : IMeasureMap
    {
        public abstract IMeasureMap Put(IMeasureDouble measure, double value);

        public abstract IMeasureMap Put(IMeasureLong measure, long value);

        public abstract void Record();

        public abstract void Record(ITagContext tags);
    }
}
