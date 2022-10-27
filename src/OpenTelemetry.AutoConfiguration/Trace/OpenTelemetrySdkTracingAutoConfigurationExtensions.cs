// <copyright file="OpenTelemetrySdkTracingAutoConfigurationExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

public static class OpenTelemetrySdkTracingAutoConfigurationExtensions
{
    public static TracerProviderBuilder UseAutoConfiguration(this TracerProviderBuilder tracerProviderBuilder)
        => UseAutoConfiguration(tracerProviderBuilder, configure: null, name: null);

    public static TracerProviderBuilder UseAutoConfiguration(this TracerProviderBuilder tracerProviderBuilder, Action<TracerProviderAutoConfigurationBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return UseAutoConfiguration(tracerProviderBuilder, configure, name: null);
    }

    public static TracerProviderBuilder UseAutoConfiguration(this TracerProviderBuilder tracerProviderBuilder, Action<TracerProviderAutoConfigurationBuilder>? configure, string? name)
    {
        Guard.ThrowIfNull(tracerProviderBuilder);

        tracerProviderBuilder.ConfigureServices(services =>
        {
            TraceSamplerDetectionHelper.ConfigureServices(services);
            TraceExporterDetectionHelper.ConfigureServices(services);
        });

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            TraceSamplerDetectionHelper.ConfigureBuilder(sp, builder, name);
            TraceExporterDetectionHelper.ConfigureBuilder(sp, builder, name);
        });

        if (configure != null)
        {
            var builder = new TracerProviderAutoConfigurationBuilder(tracerProviderBuilder);
            configure(builder);
        }

        return tracerProviderBuilder;
    }
}
