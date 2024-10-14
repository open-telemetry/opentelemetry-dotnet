// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace Microsoft.Extensions.Options;

#if NET
internal sealed class SingletonOptionsManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions> : IOptionsMonitor<TOptions>, IOptionsSnapshot<TOptions>
#else
internal sealed class SingletonOptionsManager<TOptions> : IOptionsMonitor<TOptions>, IOptionsSnapshot<TOptions>
#endif
    where TOptions : class
{
    private readonly TOptions instance;

    public SingletonOptionsManager(IOptions<TOptions> options)
    {
        this.instance = options.Value;
    }

    public TOptions CurrentValue => this.instance;

    public TOptions Value => this.instance;

    public TOptions Get(string? name) => this.instance;

    public IDisposable? OnChange(Action<TOptions, string?> listener)
        => NoopChangeNotification.Instance;

    private sealed class NoopChangeNotification : IDisposable
    {
        private NoopChangeNotification()
        {
        }

        public static NoopChangeNotification Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
