// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> originalEnvironment = [];
    private bool disposed;

    private EnvironmentVariableScope(params ReadOnlySpan<(string Name, string? Value)> environment)
    {
        foreach (var (name, value) in environment)
        {
            this.originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public static IDisposable Create(string name, string? value)
        => Create((name, value));

    public static IDisposable Create(params ReadOnlySpan<(string Name, string? Value)> environment)
        => new EnvironmentVariableScope(environment);

    public static IDisposable Create(IDictionary<string, string?> environment)
        => new EnvironmentVariableScope([.. environment.Select((p) => (p.Key, p.Value))]);

    public void Dispose()
    {
        if (!this.disposed)
        {
            foreach (var pair in this.originalEnvironment)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            this.disposed = true;
        }
    }
}
