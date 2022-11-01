// <copyright file="NoneExemplarFilter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics;

/// <summary>
/// The Exemplar Filter which never samples any measurements.
/// </summary>
internal sealed class NoneExemplarFilter : IExemplarFilter
{
    public bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        return false;
    }

    public bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        return false;
    }
}
