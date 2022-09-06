// <copyright file="OTelEnvSamplerDetector.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal class OTelEnvSamplerDetector : ISamplerDetector
{
    public const string OTelTracesSamplerEnvVarKey = "OTEL_TRACES_SAMPLER";
    public const string OTelTracesSamplerArgEnvVarKey = "OTEL_TRACES_SAMPLER_ARG";

    public Sampler Detect()
    {
        var hasValue = EnvironmentVariableHelper.LoadString(OTelTracesSamplerEnvVarKey, out var envVarValue);

        if (!hasValue)
        {
            return null;
        }

        switch (envVarValue)
        {
            case "always_on":
                return new AlwaysOnSampler();
            case "always_off":
                return new AlwaysOffSampler();
            case "traceidratio":
                return CreateTraceIdRatioBasedSampler();
            case "parentbased_always_on":
                return new ParentBasedSampler(new AlwaysOnSampler());
            case "parentbased_always_off":
                return new ParentBasedSampler(new AlwaysOffSampler());
            case "parentbased_traceidratio":
                return new ParentBasedSampler(CreateTraceIdRatioBasedSampler());
        }

        return null;
    }

    private static TraceIdRatioBasedSampler CreateTraceIdRatioBasedSampler()
    {
        const double defaultRatio = 1.0;

        var hasValue = EnvironmentVariableHelper.LoadString(OTelTracesSamplerArgEnvVarKey, out var envVarValue);

        double ratio = defaultRatio;

        if (hasValue && double.TryParse(envVarValue, out double parsedRatio) && parsedRatio >= 0.0 && parsedRatio <= 1.0)
        {
            ratio = parsedRatio;
        }

        return new TraceIdRatioBasedSampler(ratio);
    }
}
