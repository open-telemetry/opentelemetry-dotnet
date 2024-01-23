// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNetCore.Implementation;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Asp.Net Core Requests instrumentation.
/// </summary>
internal sealed class AspNetCoreInstrumentation : IDisposable
{
    private static readonly HashSet<string> DiagnosticSourceEvents = new()
    {
        "Microsoft.AspNetCore.Hosting.HttpRequestIn",
        "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start",
        "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop",
        "Microsoft.AspNetCore.Diagnostics.UnhandledException",
        "Microsoft.AspNetCore.Hosting.UnhandledException",
    };

    private readonly Func<string, object, object, bool> isEnabled = (eventName, _, _)
        => DiagnosticSourceEvents.Contains(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    public AspNetCoreInstrumentation(HttpInListener httpInListener)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(httpInListener, this.isEnabled, AspNetCoreInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
    }
}
