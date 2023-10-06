// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.Http;

internal sealed class HttpClientMetricInstrumentationOptions
{
    internal readonly HttpSemanticConvention HttpSemanticConvention;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientMetricInstrumentationOptions"/> class.
    /// </summary>
    public HttpClientMetricInstrumentationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal HttpClientMetricInstrumentationOptions(IConfiguration configuration)
    {
        Debug.Assert(configuration != null, "configuration was null");

        this.HttpSemanticConvention = GetSemanticConventionOptIn(configuration);
    }
}
