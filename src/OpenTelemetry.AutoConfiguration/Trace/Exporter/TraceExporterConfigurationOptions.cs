// <copyright file="TraceExporterConfigurationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace;

public class TraceExporterConfigurationOptions
{
    internal const string OTelTracesExporterEnvVarKey = "OTEL_TRACES_EXPORTER";

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceExporterConfigurationOptions"/> class.
    /// </summary>
    public TraceExporterConfigurationOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal TraceExporterConfigurationOptions(IConfiguration configuration)
    {
        if (configuration.TryGetStringValue(OTelTracesExporterEnvVarKey, out var value))
        {
            this.TraceExporterName = value;
        }
    }

    public string? TraceExporterName { get; set; }
}
