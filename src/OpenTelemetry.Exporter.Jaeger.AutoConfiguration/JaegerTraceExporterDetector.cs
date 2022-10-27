// <copyright file="JaegerTraceExporterDetector.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;

namespace OpenTelemetry.Trace;

internal sealed class JaegerTraceExporterDetector : ITraceExporterDetector
{
    public string Name => "jaeger";

    public BaseProcessor<Activity>? Create(IServiceProvider serviceProvider, string optionsName)
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<JaegerExporterOptions>>().Get(optionsName);

        return JaegerExporterHelperExtensions.CreateJaegerExporter(options, serviceProvider);
    }
}
