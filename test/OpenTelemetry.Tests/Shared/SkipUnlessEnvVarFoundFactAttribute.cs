// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

internal class SkipUnlessEnvVarFoundFactAttribute : FactAttribute
{
    public SkipUnlessEnvVarFoundFactAttribute(string environmentVariable)
    {
        if (string.IsNullOrEmpty(GetEnvironmentVariable(environmentVariable)))
        {
            this.Skip = $"Skipped because {environmentVariable} environment variable was not configured.";
        }
    }

    public static string GetEnvironmentVariable(string environmentVariableName)
    {
        string environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);

        if (string.IsNullOrEmpty(environmentVariableValue))
        {
            environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);
        }

        return environmentVariableValue;
    }
}
