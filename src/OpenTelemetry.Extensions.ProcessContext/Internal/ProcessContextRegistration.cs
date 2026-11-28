// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// Registered with a provider as instrumentation. Once the provider has
/// resolved its resource the payload is encoded and published; when the
/// provider shuts down (and disposes its instrumentation) the registration is
/// withdrawn. Nothing here runs on the span, metric or log record paths.
/// </summary>
internal sealed class ProcessContextRegistration : IProviderBuiltCallback, IDisposable
{
    private readonly ProcessContextOptions options;
    private readonly ProcessContextRegistry registry;
    private int disposed;

    internal ProcessContextRegistration(ProcessContextOptions options, ProcessContextRegistry registry)
    {
        this.options = options;
        this.registry = registry;
    }

    internal ProcessContextOptions Options => this.options;

    internal byte[]? Payload { get; private set; }

    public void OnProviderBuilt(BaseProvider provider)
    {
        try
        {
            if (!this.options.Enabled)
            {
                ProcessContextEventSource.Log.Disabled();
                return;
            }

            if (!this.registry.Writer.IsSupported)
            {
                ProcessContextEventSource.Log.NotSupported(Environment.OSVersion.ToString());
                return;
            }

            var resource = this.options.Resource ?? provider.GetResource();
            this.Payload = ProcessContextSerializer.Serialize(resource, attributes: null);
            this.registry.Add(this);
        }
        catch (Exception ex)
        {
            ProcessContextEventSource.Log.PublishFailedOnce(ex);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0 || this.Payload == null)
        {
            return;
        }

        try
        {
            this.registry.Remove(this);
        }
        catch (Exception ex)
        {
            ProcessContextEventSource.Log.PublishFailedOnce(ex);
        }
    }
}
