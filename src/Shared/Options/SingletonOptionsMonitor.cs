// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Microsoft.Extensions.Options;

#if NET6_0_OR_GREATER
internal sealed class SingletonOptionsMonitor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions> : IOptionsMonitor<TOptions>
#else
internal sealed class SingletonOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
#endif
    where TOptions : class
{
    private readonly TOptions instance;

    public SingletonOptionsMonitor(IOptions<TOptions> options)
    {
        this.instance = options.Value;
    }

    public TOptions CurrentValue => this.instance;

    public TOptions Get(string? name) => this.instance;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
