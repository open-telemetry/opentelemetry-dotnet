// <copyright file="TraceIdRatioBasedSamplerDetector.cs" company="OpenTelemetry Authors">
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

using System;

namespace OpenTelemetry.Trace;

internal sealed class TraceIdRatioBasedSamplerDetector : ITraceSamplerDetector
{
    public string Name => "traceidratio";

    public static Sampler CreateTraceIdRatioBasedSampler(string? argument)
    {
        const double defaultRatio = 1.0;

        double ratio = defaultRatio;

        if (!string.IsNullOrWhiteSpace(argument)
            && double.TryParse(argument, out var parsedRatio)
            && parsedRatio >= 0.0
            && parsedRatio <= 1.0)
        {
            ratio = parsedRatio;
        }

        return new TraceIdRatioBasedSampler(ratio);
    }

    public Sampler? Create(IServiceProvider serviceProvider, string optionsName, string? argument) => CreateTraceIdRatioBasedSampler(argument);
}
