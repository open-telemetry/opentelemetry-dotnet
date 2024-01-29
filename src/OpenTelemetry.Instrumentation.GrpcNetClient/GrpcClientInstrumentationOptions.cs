// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.GrpcNetClient;

/// <summary>
/// Options for GrpcClient instrumentation.
/// </summary>
public class GrpcClientInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether down stream instrumentation is suppressed (disabled).
    /// </summary>
    public bool SuppressDownstreamInstrumentation { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich the Activity with <see cref="HttpRequestMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpRequestMessage"/> object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpRequestMessage> EnrichWithHttpRequestMessage { get; set; }

    /// <summary>
    /// Gets or sets an action to enrich an Activity with <see cref="HttpResponseMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para><see cref="HttpResponseMessage"/> object from which additional information can be extracted to enrich the activity.</para>
    /// </remarks>
    public Action<Activity, HttpResponseMessage> EnrichWithHttpResponseMessage { get; set; }
}
