// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

/// <summary>
/// This <see cref="TheoryAttribute" /> skips tests if the required Docker engine is not available.
/// </summary>
internal sealed class EnabledOnDockerPlatformTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnabledOnDockerPlatformTheoryAttribute" /> class.
    /// </summary>
    public EnabledOnDockerPlatformTheoryAttribute(DockerPlatform dockerPlatform)
    {
        if (!DockerHelper.IsAvailable(dockerPlatform))
        {
            this.Skip = $"The Docker {dockerPlatform} engine is not available.";
        }
    }
}
