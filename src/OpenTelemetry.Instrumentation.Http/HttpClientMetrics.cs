// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Http;

/// <summary>
/// HttpClient instrumentation.
/// </summary>
internal sealed class HttpClientMetrics : IDisposable
{
    private static readonly HashSet<string> ExcludedDiagnosticSourceEvents = new()
    {
        "System.Net.Http.Request",
        "System.Net.Http.Response",
    };

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    private readonly Func<string, object, object, bool> isEnabled = (activityName, obj1, obj2)
        => !ExcludedDiagnosticSourceEvents.Contains(activityName);

    public HttpClientMetrics(RequestMethodHelper requestMethodHelper)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
            new HttpHandlerMetricsDiagnosticListener("HttpHandlerDiagnosticListener", requestMethodHelper),
            this.isEnabled,
            HttpInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
    }
}
