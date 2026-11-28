// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Extensions-ProcessContext")]
internal sealed class ProcessContextEventSource : EventSource, IConfigurationExtensionsLogger
{
    public static readonly ProcessContextEventSource Log = new();

    private int publishFailureLogged;

    /// <summary>
    /// Reports a failure to publish or update the process context. The first
    /// failure in the process is a warning; later ones are verbose so that a
    /// host where publication cannot work (seccomp, old kernel) is not flooded.
    /// </summary>
    /// <param name="reason">What failed.</param>
    [NonEvent]
    public void PublishFailedOnce(string reason)
    {
        if (Interlocked.Exchange(ref this.publishFailureLogged, 1) == 0)
        {
            this.PublishFailed(reason);
        }
        else
        {
            this.PublishFailedAgain(reason);
        }
    }

    [NonEvent]
    public void PublishFailedOnce(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.PublishFailedOnce(ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Process context could not be published and is not available to external readers: {0}", Level = EventLevel.Warning)]
    public void PublishFailed(string reason)
        => this.WriteEvent(1, reason);

    [Event(2, Message = "Process context could not be published again: {0}", Level = EventLevel.Verbose)]
    public void PublishFailedAgain(string reason)
        => this.WriteEvent(2, reason);

    [Event(3, Message = "Process context published: payload size = {0} bytes, memfd backed = {1}, mapping named = {2}.", Level = EventLevel.Informational)]
    public void Published(int payloadSize, bool memfdBacked, bool named)
        => this.WriteEvent(3, payloadSize, memfdBacked, named);

    [Event(4, Message = "Process context updated: payload size = {0} bytes.", Level = EventLevel.Verbose)]
    public void Updated(int payloadSize)
        => this.WriteEvent(4, payloadSize);

    [Event(5, Message = "Process context dropped.", Level = EventLevel.Verbose)]
    public void Dropped()
        => this.WriteEvent(5);

    [Event(6, Message = "Process context sharing is not supported on this platform ({0}); nothing is published.", Level = EventLevel.Verbose)]
    public void NotSupported(string platform)
        => this.WriteEvent(6, platform);

    [Event(7, Message = "Unsupported value type '{1}' for attribute '{0}'. The attribute is not published in the process context.", Level = EventLevel.Warning)]
    public void UnsupportedAttributeType(string key, string type)
        => this.WriteEvent(7, key, type);

    [Event(8, Message = "Configuration key '{0}' has an invalid value: '{1}'", Level = EventLevel.Warning)]
    public void InvalidConfigurationValue(string key, string value)
        => this.WriteEvent(8, key, value);

    [Event(9, Message = "Process context sharing is disabled for this provider. Set OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED=true or ProcessContextOptions.Enabled to enable it.", Level = EventLevel.Verbose)]
    public void Disabled()
        => this.WriteEvent(9);

    void IConfigurationExtensionsLogger.LogInvalidConfigurationValue(string key, string value)
        => this.InvalidConfigurationValue(key, value);
}
