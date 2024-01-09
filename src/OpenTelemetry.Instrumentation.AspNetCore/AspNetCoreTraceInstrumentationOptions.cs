// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Options for requests instrumentation.
/// </summary>
public class AspNetCoreTraceInstrumentationOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetCoreTraceInstrumentationOptions"/> class.
    /// </summary>
    public AspNetCoreTraceInstrumentationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal AspNetCoreTraceInstrumentationOptions(IConfiguration configuration)
    {
        Debug.Assert(configuration != null, "configuration was null");

        if (configuration.TryGetBoolValue("OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION", out var enableGrpcInstrumentation))
        {
            this.EnableGrpcAspNetCoreSupport = enableGrpcInstrumentation;
        }

        if (configuration.TryGetStringValue("OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS", out var knownHttpMethods))
        {
            var requestMethodHelper = new RequestMethodHelper(knownHttpMethods);
            this.KnownHttpMethods =
                [.. requestMethodHelper.KnownMethods.Keys];
        }
    }

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

    /// <summary>
    /// Gets or sets a value indicating whether RPC attributes are added to an Activity when using Grpc.AspNetCore.
    /// </summary>
    /// <remarks>
    /// https://github.com/open-telemetry/semantic-conventions/blob/main/docs/rpc/rpc-spans.md.
    /// </remarks>
    internal bool EnableGrpcAspNetCoreSupport { get; set; }

    internal List<string> KnownHttpMethods { get; set; } =
        [];
}
