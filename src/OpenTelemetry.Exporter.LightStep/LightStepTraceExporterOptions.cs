// <copyright file="LightStepTraceExporterOptions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Exporter.LightStep
{
    public sealed class LightStepTraceExporterOptions
    {
        public Uri Satellite { get; set; } = new Uri("https://collector.lightstep.com:443/api/v2/reports");

        public TimeSpan SatelliteTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public string ServiceName { get; set; } = "OpenTelemetry Exporter";

        public string AccessToken { get; set; }
    }
}
