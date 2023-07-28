// <copyright file="OpenTelemetryMetricsBuilderExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.Extensions.Diagnostics.Metrics;

public static class OpenTelemetryMetricsBuilderExtensions
{
    public static IMetricsBuilder AddOpenTelemetry(this IMetricsBuilder metricsBuilder)
        => AddOpenTelemetry(metricsBuilder, b => { });

    public static IMetricsBuilder AddOpenTelemetry(this IMetricsBuilder metricsBuilder, Action<MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(metricsBuilder);

        metricsBuilder.Services.AddOpenTelemetry().WithMetrics(configure);

        return metricsBuilder;
    }
}
