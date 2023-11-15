// <copyright file="KnownHistogramBuckets.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Tests;

public enum KnownHistogramBuckets
{
    /// <summary>
    /// Default OpenTelemetry semantic convention buckets.
    /// </summary>
    Default,

    /// <summary>
    /// Buckets for up to 10 seconds.
    /// </summary>
    DefaultShortSeconds,

    /// <summary>
    /// Buckets for up to 300 seconds.
    /// </summary>
    DefaultLongSeconds,
}
