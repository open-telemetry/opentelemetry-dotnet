// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - the container builds them

namespace OpenTelemetry.Configuration.Declarative.Tests;

internal sealed class TestComponent
{
    public TestComponent(ConfigProperties properties, IServiceProvider serviceProvider)
    {
        this.Properties = properties;
        this.ServiceProvider = serviceProvider;
    }

    public ConfigProperties Properties { get; }

    public IServiceProvider ServiceProvider { get; }
}

internal sealed class OtherTestComponent;

internal sealed class DisposableTestComponent : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => this.Disposed = true;
}

internal sealed class TestDependency
{
    public string Value { get; } = "injected";
}

internal sealed class TestComponentProvider : PluginComponentProvider<TestComponent>
{
    public override TestComponent Create(ConfigProperties properties, IServiceProvider serviceProvider)
        => new(properties, serviceProvider);
}

internal sealed class OtherTestComponentProvider : PluginComponentProvider<OtherTestComponent>
{
    public override OtherTestComponent Create(ConfigProperties properties, IServiceProvider serviceProvider)
        => new();
}

internal sealed class FixedNameTestComponentProvider : PluginComponentProvider<TestComponent>
{
    public const string FixedName = "fixed_name";

    public override TestComponent Create(ConfigProperties properties, IServiceProvider serviceProvider)
        => new(properties, serviceProvider);
}

internal sealed class InjectedTestComponentProvider : PluginComponentProvider<TestComponent>
{
    private readonly TestDependency dependency;

    public InjectedTestComponentProvider(TestDependency dependency)
    {
        this.dependency = dependency;
    }

    public override TestComponent Create(ConfigProperties properties, IServiceProvider serviceProvider)
    {
        _ = this.dependency.Value;
        return new(properties, serviceProvider);
    }
}

internal sealed class ThrowingTestComponentProvider : PluginComponentProvider<TestComponent>
{
    private readonly Exception exception;

    public ThrowingTestComponentProvider(Exception exception)
    {
        this.exception = exception;
    }

    public override TestComponent Create(ConfigProperties properties, IServiceProvider serviceProvider)
        => throw this.exception;
}

internal sealed class NullTestComponentProvider : PluginComponentProvider<TestComponent>
{
    public override TestComponent Create(ConfigProperties properties, IServiceProvider serviceProvider)
        => null!;
}

internal sealed class DisposableTestComponentProvider : PluginComponentProvider<DisposableTestComponent>
{
    public override DisposableTestComponent Create(ConfigProperties properties, IServiceProvider serviceProvider)
        => new();
}
