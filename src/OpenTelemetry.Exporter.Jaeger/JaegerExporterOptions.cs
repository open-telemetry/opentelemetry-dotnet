// <copyright file="JaegerExporterOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerExporterOptions
    {
        internal const string DefaultServiceName = "Open Telemetry Exporter";

        internal const int DefaultMaxPacketSize = 65000;

        /// <summary>
        /// Gets or sets the name of the service reporting telemetry. Default value: Open Telemetry Exporter.
        /// </summary>
        public string ServiceName { get; set; } = DefaultServiceName;

        /// <summary>
        /// Gets or sets the Jaeger agent host. Default value: localhost.
        /// </summary>
        public string AgentHost { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the Jaeger agent "compact thrift protocol" port. Default value: 6831.
        /// </summary>
        public int AgentPort { get; set; } = 6831;

        /// <summary>
        /// Gets or sets the maximum packet size in bytes. Default value: 65000.
        /// </summary>
        public int? MaxPacketSize { get; set; } = DefaultMaxPacketSize;

        /// <summary>
        /// Gets or sets the maximum time that should elapse between flushing the internal buffer to the configured Jaeger agent. Default value: 00:00:10.
        /// </summary>
        public TimeSpan MaxFlushInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the tags that should be sent with telemetry.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> ProcessTags { get; set; }
    }
}
