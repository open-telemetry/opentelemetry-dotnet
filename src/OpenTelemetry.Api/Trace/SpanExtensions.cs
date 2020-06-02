// <copyright file="SpanExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Helper class to populate well-known span attributes.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Helper method that populates span properties from component
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="component">Http method.</param>
        /// <returns>Span with populated http method properties.</returns>
        public static TelemetrySpan PutComponentAttribute(this TelemetrySpan span, string component)
        {
            span.SetAttribute(SpanAttributeConstants.ComponentKey, component);
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from component
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-span-general.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="peerService">Peer service.</param>
        /// <returns>Span with populated http method properties.</returns>
        public static TelemetrySpan PutPeerServiceAttribute(this TelemetrySpan span, string peerService)
        {
            span.SetAttribute(SpanAttributeConstants.PeerServiceKey, peerService);
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http method according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="method">Http method.</param>
        /// <returns>Span with populated http method properties.</returns>
        public static TelemetrySpan PutHttpMethodAttribute(this TelemetrySpan span, string method)
        {
            span.SetAttribute(SpanAttributeConstants.HttpMethodKey, method);
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http status code according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="statusCode">Http status code.</param>
        /// <returns>Span with populated status code properties.</returns>
        public static TelemetrySpan PutHttpStatusCodeAttribute(this TelemetrySpan span, int statusCode)
        {
            span.SetAttribute(SpanAttributeConstants.HttpStatusCodeKey, statusCode);
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http user agent according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="userAgent">Http status code.</param>
        /// <returns>Span with populated user agent code properties.</returns>
        public static TelemetrySpan PutHttpUserAgentAttribute(this TelemetrySpan span, string userAgent)
        {
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                span.SetAttribute(SpanAttributeConstants.HttpUserAgentKey, userAgent);
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from host and port
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="hostName">Hostr name.</param>
        /// <param name="port">Port number.</param>
        /// <returns>Span with populated host properties.</returns>
        public static TelemetrySpan PutHttpHostAttribute(this TelemetrySpan span, string hostName, int port)
        {
            if (port == 80 || port == 443)
            {
                span.SetAttribute(SpanAttributeConstants.HttpHostKey, hostName);
            }
            else
            {
                span.SetAttribute(SpanAttributeConstants.HttpHostKey, hostName + ":" + port);
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from host and port
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="hostAndPort">Host and port value.</param>
        /// <returns>Span with populated host properties.</returns>
        public static TelemetrySpan PutHttpHostAttribute(this TelemetrySpan span, string hostAndPort)
        {
            if (!string.IsNullOrEmpty(hostAndPort))
            {
                span.SetAttribute(SpanAttributeConstants.HttpHostKey, hostAndPort);
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from route
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="route">Route used to resolve url to controller.</param>
        /// <returns>Span with populated route properties.</returns>
        public static TelemetrySpan PutHttpRouteAttribute(this TelemetrySpan span, string route)
        {
            if (!string.IsNullOrEmpty(route))
            {
                span.SetAttribute(SpanAttributeConstants.HttpRouteKey, route);
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from host and port
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="rawUrl">Raw url.</param>
        /// <returns>Span with populated url properties.</returns>
        public static TelemetrySpan PutHttpRawUrlAttribute(this TelemetrySpan span, string rawUrl)
        {
            if (!string.IsNullOrEmpty(rawUrl))
            {
                span.SetAttribute(SpanAttributeConstants.HttpUrlKey, rawUrl);
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from url path according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="path">Url path.</param>
        /// <returns>Span with populated path properties.</returns>
        public static TelemetrySpan PutHttpPathAttribute(this TelemetrySpan span, string path)
        {
            span.SetAttribute(SpanAttributeConstants.HttpPathKey, path);
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http status code according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="statusCode">Http status code.</param>
        /// <param name="reasonPhrase">Http reason phrase.</param>
        /// <returns>Span with populated properties.</returns>
        public static TelemetrySpan PutHttpStatusCode(this TelemetrySpan span, int statusCode, string reasonPhrase)
        {
            span.PutHttpStatusCodeAttribute(statusCode);

            span.Status = SpanHelper.ResolveSpanStatusForHttpStatusCode(statusCode).WithDescription(reasonPhrase);

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from request version according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="flavor">HTTP version.</param>
        /// <returns>Span with populated properties.</returns>
        public static TelemetrySpan PutHttpFlavorAttribute(this TelemetrySpan span, string flavor)
        {
            span.SetAttribute(SpanAttributeConstants.HttpFlavorKey, flavor);
            return span;
        }

        /// <summary>
        /// Helper method that populates database type
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-database.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="type">Database type.</param>
        /// <returns>Span with populated properties.</returns>
        public static TelemetrySpan PutDatabaseTypeAttribute(this TelemetrySpan span, string type)
        {
            span.SetAttribute(SpanAttributeConstants.DatabaseTypeKey, type);
            return span;
        }

        /// <summary>
        /// Helper method that populates database instance
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-database.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="instance">Database instance.</param>
        /// <returns>Span with populated properties.</returns>
        public static TelemetrySpan PutDatabaseInstanceAttribute(this TelemetrySpan span, string instance)
        {
            span.SetAttribute(SpanAttributeConstants.DatabaseInstanceKey, instance);
            return span;
        }

        /// <summary>
        /// Helper method that populates database statement
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-database.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="statement">Database statement.</param>
        /// <returns>Span with populated properties.</returns>
        public static TelemetrySpan PutDatabaseStatementAttribute(this TelemetrySpan span, string statement)
        {
            span.SetAttribute(SpanAttributeConstants.DatabaseStatementKey, statement);
            return span;
        }
    }
}
