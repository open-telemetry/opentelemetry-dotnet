// <copyright file="ZipkinEndpoint.cs" company="OpenTelemetry Authors">
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
#if NET452
using Newtonsoft.Json;
#else
using System.Text.Json;
#endif

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal class ZipkinEndpoint
    {
        public ZipkinEndpoint(string serviceName)
            : this(serviceName, null, null, null)
        {
        }

        public ZipkinEndpoint(
            string serviceName,
            string ipv4,
            string ipv6,
            int? port)
        {
            this.ServiceName = serviceName;
            this.Ipv4 = ipv4;
            this.Ipv6 = ipv6;
            this.Port = port;
        }

        public string ServiceName { get; }

        public string Ipv4 { get; }

        public string Ipv6 { get; }

        public int? Port { get; }

        public static ZipkinEndpoint Create(string serviceName)
        {
            return new ZipkinEndpoint(serviceName);
        }

#if !NET452
        public static ZipkinEndpoint Create((string name, int port) serviceNameAndPort)
        {
            var serviceName = serviceNameAndPort.port == default
                ? serviceNameAndPort.name
                : $"{serviceNameAndPort.name}:{serviceNameAndPort.port}";

            return new ZipkinEndpoint(serviceName);
        }
#endif

        public ZipkinEndpoint Clone(string serviceName)
        {
            return new ZipkinEndpoint(
                serviceName,
                this.Ipv4,
                this.Ipv6,
                this.Port);
        }

#if NET452
        public void Write(JsonWriter writer)
        {
            writer.WriteStartObject();

            if (this.ServiceName != null)
            {
                writer.WritePropertyName("serviceName");
                writer.WriteValue(this.ServiceName);
            }

            if (this.Ipv4 != null)
            {
                writer.WritePropertyName("ipv4");
                writer.WriteValue(this.Ipv4);
            }

            if (this.Ipv6 != null)
            {
                writer.WritePropertyName("ipv6");
                writer.WriteValue(this.Ipv6);
            }

            if (this.Port.HasValue)
            {
                writer.WritePropertyName("port");
                writer.WriteValue(this.Port.Value);
            }

            writer.WriteEndObject();
        }
#else
        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            if (this.ServiceName != null)
            {
                writer.WriteString("serviceName", this.ServiceName);
            }

            if (this.Ipv4 != null)
            {
                writer.WriteString("ipv4", this.Ipv4);
            }

            if (this.Ipv6 != null)
            {
                writer.WriteString("ipv6", this.Ipv6);
            }

            if (this.Port.HasValue)
            {
                writer.WriteNumber("port", this.Port.Value);
            }

            writer.WriteEndObject();
        }
#endif
    }
}
