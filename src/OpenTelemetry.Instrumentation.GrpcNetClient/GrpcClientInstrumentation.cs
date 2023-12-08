// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;

namespace OpenTelemetry.Instrumentation.GrpcNetClient;

/// <summary>
/// GrpcClient instrumentation.
/// </summary>
internal sealed class GrpcClientInstrumentation : IDisposable
{
    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcClientInstrumentation"/> class.
    /// </summary>
    /// <param name="options">Configuration options for Grpc client instrumentation.</param>
    public GrpcClientInstrumentation(GrpcClientInstrumentationOptions options = null)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new GrpcClientDiagnosticListener(options), isEnabledFilter: null, GrpcInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber.Dispose();
    }
}
