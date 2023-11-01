// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Options for metrics requests instrumentation.
/// </summary>
internal sealed class AspNetCoreMetricsInstrumentationOptions
{
    internal readonly HttpSemanticConvention HttpSemanticConvention;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetCoreMetricsInstrumentationOptions"/> class.
    /// </summary>
    public AspNetCoreMetricsInstrumentationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal AspNetCoreMetricsInstrumentationOptions(IConfiguration configuration)
    {
        Debug.Assert(configuration != null, "configuration was null");

        this.HttpSemanticConvention = GetSemanticConventionOptIn(configuration);
    }
}

