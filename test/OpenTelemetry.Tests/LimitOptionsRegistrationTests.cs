// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests;

public sealed class LimitOptionsRegistrationTests
{
    [Fact]
    public void GeneralOptionsAreConfiguredOnceWhenComposingSignalOptions()
    {
        var invocations = 0;

        var services = CreateServices();
        services.Configure<AttributeLimitOptions>(options =>
        {
            invocations++;
            options.AttributeCountLimit = 10;
        });

        using var serviceProvider = services.BuildServiceProvider();

        var attributeLimitOptions = serviceProvider.GetRequiredService<IOptionsMonitor<AttributeLimitOptions>>().CurrentValue;
        var spanLimitOptions = serviceProvider.GetRequiredService<IOptionsMonitor<SpanLimitOptions>>().CurrentValue;
        var logRecordLimitOptions = serviceProvider.GetRequiredService<IOptionsMonitor<LogRecordLimitOptions>>().CurrentValue;

        Assert.Equal(1, invocations);
        Assert.Equal(10, attributeLimitOptions.AttributeCountLimit);
        Assert.Equal(10, spanLimitOptions.AttributeCountLimit);
        Assert.Equal(10, logRecordLimitOptions.AttributeCountLimit);
    }

    [Fact]
    public void AttributeLimitOptionsAreNotReloaded()
        => AssertOptionsAreNotReloaded<AttributeLimitOptions>();

    [Fact]
    public void SpanLimitOptionsAreNotReloaded()
        => AssertOptionsAreNotReloaded<SpanLimitOptions>();

    [Fact]
    public void LogRecordLimitOptionsAreNotReloaded()
        => AssertOptionsAreNotReloaded<LogRecordLimitOptions>();

    private static void AssertOptionsAreNotReloaded<T>()
        where T : class
    {
        var invocations = 0;
        var namedInvocations = 0;
        using var changeTokenSource = new TestChangeTokenSource<T>();

        var services = CreateServices();
        services.AddSingleton<IOptionsChangeTokenSource<T>>(changeTokenSource);
        services.Configure<T>(options => invocations++);
        services.Configure<T>("custom", options => namedInvocations++);

        using var serviceProvider = services.BuildServiceProvider();

        var monitor = serviceProvider.GetRequiredService<IOptionsMonitor<T>>();
        var initial = monitor.CurrentValue;

        var changeNotifications = 0;
        using var listener = monitor.OnChange((options, name) => changeNotifications++);

        changeTokenSource.TriggerChange();

        Assert.Same(initial, monitor.CurrentValue);
        Assert.Same(initial, monitor.Get("custom"));
        Assert.Equal(1, invocations);
        Assert.Equal(0, namedInvocations);
        Assert.Equal(0, changeNotifications);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.AddOpenTelemetryTracerProviderBuilderServices();
        services.AddOpenTelemetryLoggerProviderBuilderServices();
        return services;
    }

    private sealed class TestChangeTokenSource<T> : IOptionsChangeTokenSource<T>, IDisposable
    {
        private CancellationTokenSource cancellationTokenSource = new();

        public string? Name => Options.DefaultName;

        public IChangeToken GetChangeToken()
            => new CancellationChangeToken(this.cancellationTokenSource.Token);

        public void TriggerChange()
        {
            var previous = Interlocked.Exchange(ref this.cancellationTokenSource, new CancellationTokenSource());
            previous.Cancel();
            previous.Dispose();
        }

        public void Dispose()
            => this.cancellationTokenSource.Dispose();
    }
}
