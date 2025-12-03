// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string name;
    private readonly string? previous;

    public EnvironmentVariableScope(string name, string? value)
    {
        this.name = name;
        this.previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(this.name, this.previous);
}
