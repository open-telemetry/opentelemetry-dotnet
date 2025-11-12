// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable IDE0005 // Using directive is unnecessary.
using Xunit;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace OpenTelemetry.Tests;

internal sealed class SkipUnlessEnvVarFoundTheoryAttribute : TheoryAttribute
{
    public SkipUnlessEnvVarFoundTheoryAttribute(string environmentVariable)
    {
        this.EnvironmentVariable = environmentVariable;
        if (string.IsNullOrEmpty(GetEnvironmentVariable(environmentVariable)))
        {
            this.Skip = $"Skipped because {environmentVariable} environment variable was not configured.";
        }
    }

    public string EnvironmentVariable { get; }

    public static string? GetEnvironmentVariable(string environmentVariableName)
    {
        string? environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);

        if (string.IsNullOrEmpty(environmentVariableValue))
        {
            environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);
        }

        return environmentVariableValue;
    }
}
