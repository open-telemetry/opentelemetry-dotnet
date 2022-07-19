// <copyright file="ExponentialBucketHistogram.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Represents an exponential bucket histogram with base = 2 ^ (2 ^ (-scale))
    /// </summary>
    public class ExponentialBucketHistogram
    {
        internal static readonly double Log2E = 1 / Math.Log(2); // Math.Log2(Math.E)

        private int scale;
    }
}
