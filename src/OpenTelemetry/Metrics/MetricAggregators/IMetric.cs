// <copyright file="IMetric.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    public interface IMetric
    {
        string Name { get; }

        string Description { get; }

        string Unit { get; }

        Meter Meter { get; }

        DateTimeOffset StartTimeExclusive { get; }

        DateTimeOffset EndTimeInclusive { get; }

        KeyValuePair<string, object>[] Attributes { get; }

        string ToDisplayString();
    }
}
