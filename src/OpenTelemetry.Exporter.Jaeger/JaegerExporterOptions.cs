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

using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerExporterOptions
    {
        internal const string DefaultServiceName = "OpenTelemetry Exporter";

        internal const int DefaultMaxPayloadSizeInBytes = 4096;

        /// <summary>
        /// Gets or sets the name of the service reporting telemetry. Default value: OpenTelemetry Exporter.
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
        /// Gets or sets the maximum payload size in bytes. Default value: 4096.
        /// </summary>
        public int? MaxPayloadSizeInBytes { get; set; } = DefaultMaxPayloadSizeInBytes;

        /// <summary>
        /// Gets or sets the tags that should be sent with telemetry.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> ProcessTags { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not a batch should be sent to the Jaeger agent for each service. Default value: true.
        /// </summary>
        /// <remarks>
        /// Jaeger UI will only detect &amp; color dependency spans when both
        /// the client &amp; server processes report data to the same Jaeger
        /// instance. For processes that make calls to non-instrumented services
        /// (third parties, SQL, legacy systems, etc.) the
        /// GenerateServiceSpecificBatches flag is provided to trick Jaeger into
        /// correctly detecting and displaying all dependent spans as if both
        /// sides reported data. For more details, see <a
        /// href="https://github.com/jaegertracing/jaeger-ui/issues/594">jaeger-ui/issues/594</a>.
        /// </remarks>
        public bool GenerateServiceSpecificBatches { get; set; } = true;
    }
}
