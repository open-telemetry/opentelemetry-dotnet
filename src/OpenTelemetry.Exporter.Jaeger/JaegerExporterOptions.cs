// <copyright file="JaegerExporterOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerExporterOptions
    {
        internal const int DefaultMaxPacketSize = 65000;

        public string ServiceName { get; set; }

        public string AgentHost { get; set; } = "localhost";

        public int AgentPort { get; set; } = 6831;

        public int? MaxPacketSize { get; set; } = DefaultMaxPacketSize;

        public TimeSpan MaxFlushInterval { get; set; } = TimeSpan.FromSeconds(10);

        public Dictionary<string, object> ProcessTags { get; set; }
    }
}
