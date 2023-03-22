// <copyright file="SkipOnWindowsRunnerTheoryAttribute.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Tests;

/// <summary>
/// The GitHub-hosted Windows runners do not support Linux containers. This <see cref="TheoryAttribute" /> skips tests running on Windows runners.
/// </summary>
internal class SkipOnWindowsRunnerTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkipOnWindowsRunnerTheoryAttribute" /> class.
    /// </summary>
    public SkipOnWindowsRunnerTheoryAttribute()
    {
        if ("Windows".Equals(Environment.GetEnvironmentVariable("RUNNER_OS"), StringComparison.OrdinalIgnoreCase))
        {
            this.Skip = "The Docker Linux engine is not available on GitHub-hosted Windows runners.";
        }
    }
}
