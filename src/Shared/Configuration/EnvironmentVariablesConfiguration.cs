// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Microsoft.Extensions.Configuration;

internal static class EnvironmentVariablesConfiguration
{
    public static IConfigurationRoot Create() => new ConfigurationBuilder()
        .Add(new EnvironmentVariablesSource())
        .Build();

    private sealed class EnvironmentVariablesSource : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new EnvironmentVariablesProvider();
    }

    private sealed class EnvironmentVariablesProvider : EnvironmentVariablesConfigurationProvider
    {
        public override void Load()
        {
            try
            {
                base.Load();
            }
            catch (System.Security.SecurityException)
            {
                // Keep the last successful snapshot (initially empty) and the source so a later
                // successful reload can read the environment again.
            }
        }
    }
}
