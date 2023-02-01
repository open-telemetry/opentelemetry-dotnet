// <copyright file="SdkExperimentalOptions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Contains experimental options and feature switches for the OpenTelemetry
/// SDK.
/// </summary>
/// <remarks>
/// Note: Use at your own risk. These options and features switches...
/// <list type="bullet">
/// <item>May change or be removed in future versions.</item>
/// <item>May tweak behaviors which could introduce incompatibility with SDK
/// components and/or backends.</item>
/// </list>
/// </remarks>
internal sealed class SdkExperimentalOptions
{
    public const string MetricNameValidationConfigurationKey = "OTEL_DOTNET_EXPERIMENTAL_METRICNAMEVALIDATIONREGEX";
    private const string DefaultMetricNameValidationRegex = @"^[a-z][a-z0-9-._]{0,62}$";

    private string metricNameValidationRegex;

    public SdkExperimentalOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    public SdkExperimentalOptions(IConfiguration configuration)
    {
        Guard.ThrowIfNull(configuration);

        this.metricNameValidationRegex = configuration.GetValue(MetricNameValidationConfigurationKey, DefaultMetricNameValidationRegex);
    }

    /// <summary>
    /// Gets or sets the regular expression used to validate instrument and view
    /// names in metrics. Default value: ^[a-z][a-z0-9-._]{0,62}$.
    /// </summary>
    /// <remarks>
    /// Note: The default value is what the OpenTelemetry Specification defines
    /// and is guaranteed to work for Prometheus and OTLP exporters. This value
    /// may be changed but may introduce problems in exporters and/or backends.
    /// Use at your own risk.
    /// </remarks>
    public string MetricNameValidationRegex
    {
        get => this.metricNameValidationRegex;
        set
        {
            Guard.ThrowIfNullOrWhitespace(value);

            this.metricNameValidationRegex = value.Trim();
        }
    }
}
