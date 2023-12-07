// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Options for requests instrumentation.
/// </summary>
public class AspNetCoreTraceInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a filter function that determines whether or not to
    /// collect telemetry on a per request basis.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>The return value for the filter function is interpreted as:
    /// <list type="bullet">
    /// <item>If filter returns <see langword="true" />, the request is
    /// collected.</item>
    /// <item>If filter returns <see langword="false" /> or throws an
    /// exception the request is NOT collected.</item>
    /// </list></item>
    /// </list>
    /// </remarks>
    public Func<HttpContext, bool> Filter { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpRequest"/>: the HttpRequest object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpRequest> EnrichWithHttpRequest { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpResponse"/>: the HttpResponse object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpResponse> EnrichWithHttpResponse { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="Exception"/>: the Exception object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, Exception> EnrichWithException { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exception will be recorded as ActivityEvent or not.
    /// </summary>
    /// <remarks>
    /// https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md.
    /// </remarks>
    public bool RecordException { get; set; }

    /*
     * Removing for stable release of http instrumentation.
     * grpc semantic conventions are not yet stable so this option will not be part of stable package.
    #if NET6_0_OR_GREATER
        /// <summary>
        /// Gets or sets a value indicating whether RPC attributes are added to an Activity when using Grpc.AspNetCore. Default is true.
        /// </summary>
        /// <remarks>
        /// https://github.com/open-telemetry/semantic-conventions/blob/main/docs/rpc/rpc-spans.md.
        /// </remarks>
        public bool EnableGrpcAspNetCoreSupport { get; set; } = true;
    #endif
    */
}