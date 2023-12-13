// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

/// <summary>
/// Telemetry middleware.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TelemetryMiddleware"/> class.
/// </remarks>
/// <param name="options">trace options.</param>
public class TelemetryMiddleware(AspNetCoreTraceInstrumentationOptions options) : IMiddleware
{
    internal readonly HttpInListener HttpInListener = new HttpInListener(options);

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            this.HttpInListener.OnEventWritten("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", context);
            await next(context).ConfigureAwait(false);
            this.HttpInListener.OnEventWritten("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", context);
        }
        catch
        {
            // Exception
        }
    }
}
