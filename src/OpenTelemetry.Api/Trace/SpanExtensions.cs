// <copyright file="SpanExtensions.cs" company="OpenTelemetry Authors">
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
        public static ISpan PutComponentAttribute(this ISpan span, string component)
        {
            span.SetAttribute(SpanAttributeConstants.ComponentKey, component);
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http method according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="method">Http method.</param>
        /// <returns>Span with populated http method properties.</returns>
        public static ISpan PutHttpMethodAttribute(this ISpan span, string method)
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
        public static ISpan PutHttpStatusCodeAttribute(this ISpan span, int statusCode)
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
        public static ISpan PutHttpUserAgentAttribute(this ISpan span, string userAgent)
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
        public static ISpan PutHttpHostAttribute(this ISpan span, string hostName, int port)
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
        /// Helper method that populates span properties from route
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="route">Route used to resolve url to controller.</param>
        /// <returns>Span with populated route properties.</returns>
        public static ISpan PutHttpRouteAttribute(this ISpan span, string route)
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
        public static ISpan PutHttpRawUrlAttribute(this ISpan span, string rawUrl)
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
        public static ISpan PutHttpPathAttribute(this ISpan span, string path)
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
        public static ISpan PutHttpStatusCode(this ISpan span, int statusCode, string reasonPhrase)
        {
            span.PutHttpStatusCodeAttribute(statusCode);

            var newStatus = Status.Ok;

            if ((int)statusCode < 200)
            {
                newStatus = Status.Unknown;
            }
            else if ((int)statusCode >= 200 && (int)statusCode <= 399)
            {
                newStatus = Status.Ok;
            }
            else if ((int)statusCode == 400)
            {
                newStatus = Status.InvalidArgument;
            }
            else if ((int)statusCode == 401)
            {
                newStatus = Status.Unauthenticated;
            }
            else if ((int)statusCode == 403)
            {
                newStatus = Status.PermissionDenied;
            }
            else if ((int)statusCode == 404)
            {
                newStatus = Status.NotFound;
            }
            else if ((int)statusCode == 429)
            {
                newStatus = Status.ResourceExhausted;
            }
            else if ((int)statusCode == 501)
            {
                newStatus = Status.Unimplemented;
            }
            else if ((int)statusCode == 503)
            {
                newStatus = Status.Unavailable;
            }
            else if ((int)statusCode == 504)
            {
                newStatus = Status.DeadlineExceeded;
            }
            else
            {
                newStatus = Status.Unknown;
            }

            span.Status = newStatus.WithDescription(reasonPhrase);

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from request version according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="flavor">HTTP version.</param>
        /// <returns>Span with populated request size properties.</returns>
        public static ISpan PutHttpFlavorAttribute(this ISpan span, string flavor)
        {
            span.SetAttribute(SpanAttributeConstants.HttpFlavorKey, flavor);
            return span;
        }
    }
}
