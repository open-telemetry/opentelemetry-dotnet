// <copyright file="SamplerBuilderExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace;

public static class SamplerBuilderExtensions
{
    /// <summary>
    /// Adds sampler parsed from OTEL_TRACES_SAMPLER, OTEL_TRACES_SAMPLER_ARG environment variables
    /// to a <see cref="SamplerBuilder"/> following the <a
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#general-sdk-configuration">General
    /// SDK Configuration</a>.
    /// Supports following values for OTEL_TRACES_SAMPLER: always_on, always_off, traceidratio, parentbased_always_on, parentbased_always_off, parentbased_traceidratio.
    /// </summary>
    /// <param name="samplerBuilder"><see cref="SamplerBuilder"/>.</param>
    /// <returns>Returns <see cref="SamplerBuilder"/> for chaining.</returns>
    public static SamplerBuilder RegisterEnvironmentVariableDetector(this SamplerBuilder samplerBuilder)
    {
        return samplerBuilder.RegisterDetector(new OTelEnvSamplerDetector());
    }
}
