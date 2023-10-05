// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Http.Implementation;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientMetrics"/> class.
    /// </summary>
    /// <param name="options">HttpClient metric instrumentation options.</param>
    public HttpClientMetrics(HttpClientMetricInstrumentationOptions options)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(
            new HttpHandlerMetricsDiagnosticListener("HttpHandlerDiagnosticListener", options),
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
