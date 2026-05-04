// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

/// <summary>
/// This <see cref="FactAttribute" /> skips tests if the required Docker engine is not available.
/// </summary>
internal sealed class EnabledOnDockerPlatformFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnabledOnDockerPlatformFactAttribute" /> class.
    /// </summary>
    public EnabledOnDockerPlatformFactAttribute(DockerPlatform dockerPlatform)
    {
        if (!DockerHelper.IsAvailable(dockerPlatform))
        {
            this.Skip = $"The Docker {dockerPlatform} engine is not available.";
        }
    }
}
