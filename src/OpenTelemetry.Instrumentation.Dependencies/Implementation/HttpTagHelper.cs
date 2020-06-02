// <copyright file="HttpTagHelper.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    /// <summary>
    /// A collection of helper methods to be used when building Http spans.
    /// </summary>
    public static class HttpTagHelper
    {
        private static readonly ConcurrentDictionary<string, string> MethodOperationNameCache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<HttpMethod, string> HttpMethodOperationNameCache = new ConcurrentDictionary<HttpMethod, string>();
        private static readonly ConcurrentDictionary<HttpMethod, string> HttpMethodNameCache = new ConcurrentDictionary<HttpMethod, string>();
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<int, string>> HostAndPortToStringCache = new ConcurrentDictionary<string, ConcurrentDictionary<int, string>>();
        private static readonly ConcurrentDictionary<Version, string> ProtocolVersionToStringCache = new ConcurrentDictionary<Version, string>();
        private static readonly ConcurrentDictionary<HttpStatusCode, string> StatusCodeToStringCache = new ConcurrentDictionary<HttpStatusCode, string>();

        private static readonly Func<string, string> ConvertMethodToOperationNameRef = ConvertMethodToOperationName;
        private static readonly Func<HttpMethod, string> ConvertHttpMethodToOperationNameRef = ConvertHttpMethodToOperationName;
        private static readonly Func<HttpMethod, string> ConvertHttpMethodToNameRef = ConvertHttpMethodToName;
        private static readonly Func<Version, string> ConvertConvertProtcolVersionToStringRef = ConvertProtcolVersionToString;
        private static readonly Func<HttpStatusCode, string> ConvertHttpStatusCodeToStringRef = ConvertHttpStatusCodeToString;

        /// <summary>
        /// Gets the OpenTelemetry standard operation name for a span based on its Http method.
        /// </summary>
        /// <param name="method">Http method.</param>
        /// <returns>Span operation name.</returns>
        public static string GetOperationNameForHttpMethod(string method) => MethodOperationNameCache.GetOrAdd(method, ConvertMethodToOperationNameRef);

        /// <summary>
        /// Gets the OpenTelemetry standard operation name for a span based on its <see cref="HttpMethod"/>.
        /// </summary>
        /// <param name="method"><see cref="HttpMethod"/>.</param>
        /// <returns>Span operation name.</returns>
        public static string GetOperationNameForHttpMethod(HttpMethod method) => HttpMethodOperationNameCache.GetOrAdd(method, ConvertHttpMethodToOperationNameRef);

        /// <summary>
        /// Gets the OpenTelemetry standard method name for a span based on its <see cref="HttpMethod"/>.
        /// </summary>
        /// <param name="method"><see cref="HttpMethod"/>.</param>
        /// <returns>Span method name.</returns>
        public static string GetNameForHttpMethod(HttpMethod method) => HttpMethodNameCache.GetOrAdd(method, ConvertHttpMethodToNameRef);

        /// <summary>
        /// Gets the OpenTelemetry standard version tag value for a span based on its protocol <see cref="Version"/>.
        /// </summary>
        /// <param name="protocolVersion"><see cref="Version"/>.</param>
        /// <returns>Span flavor value.</returns>
        public static string GetFlavorTagValueFromProtocolVersion(Version protocolVersion) => ProtocolVersionToStringCache.GetOrAdd(protocolVersion, ConvertConvertProtcolVersionToStringRef);

        /// <summary>
        /// Gets the OpenTelemetry standard status code tag value for a span based on its protocol <see cref="HttpStatusCode"/>.
        /// </summary>
        /// <param name="statusCode"><see cref="HttpStatusCode"/>.</param>
        /// <returns>Span status code value.</returns>
        public static string GetStatusCodeTagValueFromHttpStatusCode(HttpStatusCode statusCode) => StatusCodeToStringCache.GetOrAdd(statusCode, ConvertHttpStatusCodeToStringRef);

        /// <summary>
        /// Gets the OpenTelemetry standard host tag value for a span based on its request <see cref="Uri"/>.
        /// </summary>
        /// <param name="requestUri"><see cref="Uri"/>.</param>
        /// <returns>Span host value.</returns>
        public static string GetHostTagValueFromRequestUri(Uri requestUri)
        {
            string host = requestUri.Host;

            if (requestUri.IsDefaultPort)
            {
                return host;
            }

            int port = requestUri.Port;

            if (!HostAndPortToStringCache.TryGetValue(host, out ConcurrentDictionary<int, string> portCache))
            {
                portCache = new ConcurrentDictionary<int, string>();
                HostAndPortToStringCache.TryAdd(host, portCache);
            }

            if (!portCache.TryGetValue(port, out string hostTagValue))
            {
                hostTagValue = $"{requestUri.Host}:{requestUri.Port}";
                portCache.TryAdd(port, hostTagValue);
            }

            return hostTagValue;
        }

        private static string ConvertMethodToOperationName(string method) => $"HTTP {method}";

        private static string ConvertHttpMethodToOperationName(HttpMethod method) => $"HTTP {method}";

        private static string ConvertHttpMethodToName(HttpMethod method) => method.ToString();

        private static string ConvertHttpStatusCodeToString(HttpStatusCode statusCode) => ((int)statusCode).ToString();

        private static string ConvertProtcolVersionToString(Version protocolVersion) => protocolVersion.ToString();
    }
}
