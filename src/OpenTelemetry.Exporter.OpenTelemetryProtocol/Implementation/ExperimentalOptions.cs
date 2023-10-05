// <copyright file="ExperimentalOptions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class ExperimentalOptions
{
    public const string EMITLOGEXCEPTIONATTRIBUTES = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES";

    public const string EMITCATEGORYANDEVENTATTRIBUTES = "OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_CATEGORY_AND_EVENT_LOG_ATTRIBUTES";

    public ExperimentalOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    public ExperimentalOptions(IConfiguration configuration)
    {
        if (configuration.TryGetBoolValue(EMITLOGEXCEPTIONATTRIBUTES, out var emitLogExceptionAttributes))
        {
            this.EmitLogExceptionAttributes = emitLogExceptionAttributes;
        }

        if (configuration.TryGetBoolValue(EMITCATEGORYANDEVENTATTRIBUTES, out var emitLogEventAndCategoryAttributes))
        {
            this.EmitLogEventAndCategoryAttributes = emitLogEventAndCategoryAttributes;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether log exception attributes should be exported.
    /// </summary>
    public bool EmitLogExceptionAttributes { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether log event and category attributes should be exported.
    /// </summary>
    public bool EmitLogEventAndCategoryAttributes { get; set; } = false;
}
