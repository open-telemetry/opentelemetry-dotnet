// <copyright file="HttpSemanticConventionHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Internal;

/// <summary>
/// Helper class for Http Semantic Conventions.
/// </summary>
/// <remarks>
/// Due to a breaking change in the semantic convention, affected instrumentation libraries
/// must inspect an environment variable to determine which attributes to emit.
/// This is expected to be removed when the instrumentation libraries reach Stable.
/// <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md"/>.
/// </remarks>
internal static class HttpSemanticConventionHelper
{
    [Flags]
    internal enum HttpSemanticConvention
    {
        /// <summary>
        /// Instructs an instrumentation library to emit the old experimental HTTP attributes.
        /// </summary>
        Old = 0x1,

        /// <summary>
        /// Instructs an instrumentation library to emit the new, stable Http attributes.
        /// </summary>
        New = 0x2,

        /// <summary>
        /// Instructs an instrumentation library to emit both the old and new attributes.
        /// </summary>
        Dupe = Old | New,
    }

    public static HttpSemanticConvention GetSemanticConventionOptIn()
    {
        try
        {
            var envVarValue = Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN");
            return envVarValue?.ToLowerInvariant() switch
            {
                "http" => HttpSemanticConvention.New,
                "http/dup" => HttpSemanticConvention.Dupe,
                _ => HttpSemanticConvention.Old,
            };
        }
        catch
        {
            return HttpSemanticConvention.Old;
        }
    }
}
