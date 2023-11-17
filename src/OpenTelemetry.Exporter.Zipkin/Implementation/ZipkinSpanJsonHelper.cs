// <copyright file="ZipkinSpanJsonHelper.cs" company="OpenTelemetry Authors">
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

using System.Text.Json;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal static class ZipkinSpanJsonHelper
{
    public static readonly JsonEncodedText TraceIdPropertyName = JsonEncodedText.Encode("traceId");

    public static readonly JsonEncodedText NamePropertyName = JsonEncodedText.Encode("name");

    public static readonly JsonEncodedText ParentIdPropertyName = JsonEncodedText.Encode("parentId");

    public static readonly JsonEncodedText IdPropertyName = JsonEncodedText.Encode("id");

    public static readonly JsonEncodedText KindPropertyName = JsonEncodedText.Encode("kind");

    public static readonly JsonEncodedText TimestampPropertyName = JsonEncodedText.Encode("timestamp");

    public static readonly JsonEncodedText DurationPropertyName = JsonEncodedText.Encode("duration");

    public static readonly JsonEncodedText DebugPropertyName = JsonEncodedText.Encode("debug");

    public static readonly JsonEncodedText SharedPropertyName = JsonEncodedText.Encode("shared");

    public static readonly JsonEncodedText LocalEndpointPropertyName = JsonEncodedText.Encode("localEndpoint");

    public static readonly JsonEncodedText RemoteEndpointPropertyName = JsonEncodedText.Encode("remoteEndpoint");

    public static readonly JsonEncodedText AnnotationsPropertyName = JsonEncodedText.Encode("annotations");

    public static readonly JsonEncodedText ValuePropertyName = JsonEncodedText.Encode("value");

    public static readonly JsonEncodedText TagsPropertyName = JsonEncodedText.Encode("tags");

    public static readonly JsonEncodedText ServiceNamePropertyName = JsonEncodedText.Encode("serviceName");

    public static readonly JsonEncodedText Ipv4PropertyName = JsonEncodedText.Encode("ipv4");

    public static readonly JsonEncodedText Ipv6PropertyName = JsonEncodedText.Encode("ipv6");

    public static readonly JsonEncodedText PortPropertyName = JsonEncodedText.Encode("port");
}
