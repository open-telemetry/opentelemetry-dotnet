// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Tests;

internal sealed class SkipUnlessEnvVarFoundFactAttribute : FactAttribute
{
    public SkipUnlessEnvVarFoundFactAttribute(
        string environmentVariable,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
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
        var environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Process);

        if (string.IsNullOrEmpty(environmentVariableValue))
        {
            environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);
        }

        return environmentVariableValue;
    }
}
