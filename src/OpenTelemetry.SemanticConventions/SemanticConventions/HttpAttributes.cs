// <copyright file="HttpAttributes.cs" company="OpenTelemetry Authors">
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

// <auto-generated> This file has been auto generated from buildscripts/semantic-conventions/templates/Attributes.cs.j2</auto-generated>

using System;

namespace OpenTelemetry.SemanticConventions
{
    /// <summary>
    /// Describes semantic conventions for attributes in the <c>http</c> namespace.
    /// </summary>
    public static class HttpAttributes
    {
        /// <summary>
        /// HTTP request headers, <c><key></c> being the normalized HTTP Header name (lowercase), the value being the header values.
        /// </summary>
        /// <remarks>
        /// Instrumentations SHOULD require an explicit configuration of which headers are to be captured. Including all request headers can be a security risk - explicit configuration helps avoid leaking sensitive information.
        /// The <c>User-Agent</c> header is already captured in the <c>user_agent.original</c> attribute. Users MAY explicitly configure instrumentations to capture them even though it is not recommended.
        /// The attribute value MUST consist of either multiple header values as an array of strings or a single-item array containing a possibly comma-concatenated string, depending on the way the HTTP library provides access to headers.
        /// </remarks>
        public const string HttpRequestHeaderTemplate = "http.request.header";

        /// <summary>
        /// HTTP request method.
        /// </summary>
        /// <remarks>
        /// HTTP request method value SHOULD be &amp;#34;known&amp;#34; to the instrumentation.
        /// By default, this convention defines &amp;#34;known&amp;#34; methods as the ones listed in <a href="https://www.rfc-editor.org/rfc/rfc9110.html#name-methods">RFC9110</a>
        /// and the PATCH method defined in <a href="https://www.rfc-editor.org/rfc/rfc5789.html">RFC5789</a>.If the HTTP request method is not known to instrumentation, it MUST set the <c>http.request.method</c> attribute to <c>_OTHER</c>.If the HTTP instrumentation could end up converting valid HTTP request methods to <c>_OTHER</c>, then it MUST provide a way to override
        /// the list of known HTTP methods. If this override is done via environment variable, then the environment variable MUST be named
        /// OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS and support a comma-separated list of case-sensitive known HTTP methods
        /// (this list MUST be a full override of the default known method, it is not a list of known methods in addition to the defaults).HTTP method names are case-sensitive and <c>http.request.method</c> attribute value MUST match a known HTTP method name exactly.
        /// Instrumentations for specific web frameworks that consider HTTP methods to be case insensitive, SHOULD populate a canonical equivalent.
        /// Tracing instrumentations that do so, MUST also set <c>http.request.method_original</c> to the original value.
        /// </remarks>
        public const string HttpRequestMethod = "http.request.method";

        /// <summary>
        /// Original HTTP method sent by the client in the request line.
        /// </summary>
        public const string HttpRequestMethodOriginal = "http.request.method_original";

        /// <summary>
        /// The ordinal number of request resending attempt (for any reason, including redirects).
        /// </summary>
        /// <remarks>
        /// The resend count SHOULD be updated each time an HTTP request gets resent by the client, regardless of what was the cause of the resending (e.g. redirection, authorization failure, 503 Server Unavailable, network issues, or any other).
        /// </remarks>
        public const string HttpRequestResendCount = "http.request.resend_count";

        /// <summary>
        /// HTTP response headers, <c><key></c> being the normalized HTTP Header name (lowercase), the value being the header values.
        /// </summary>
        /// <remarks>
        /// Instrumentations SHOULD require an explicit configuration of which headers are to be captured. Including all response headers can be a security risk - explicit configuration helps avoid leaking sensitive information.
        /// Users MAY explicitly configure instrumentations to capture them even though it is not recommended.
        /// The attribute value MUST consist of either multiple header values as an array of strings or a single-item array containing a possibly comma-concatenated string, depending on the way the HTTP library provides access to headers.
        /// </remarks>
        public const string HttpResponseHeaderTemplate = "http.response.header";

        /// <summary>
        /// <a href="https://tools.ietf.org/html/rfc7231#section-6">HTTP response status code</a>.
        /// </summary>
        public const string HttpResponseStatusCode = "http.response.status_code";

        /// <summary>
        /// The matched route, that is, the path template in the format used by the respective server framework.
        /// </summary>
        /// <remarks>
        /// MUST NOT be populated when this is not supported by the HTTP server framework as the route attribute should have low-cardinality and the URI path can NOT substitute it.
        /// SHOULD include the <a href="/docs/http/http-spans.md#http-server-definitions">application root</a> if there is one.
        /// </remarks>
        public const string HttpRoute = "http.route";

        /// <summary>
        /// HTTP request method.
        /// </summary>
        public static class HttpRequestMethodValues
        {
            /// <summary>
            /// CONNECT method.
            /// </summary>
            public const string Connect = "CONNECT";
            /// <summary>
            /// DELETE method.
            /// </summary>
            public const string Delete = "DELETE";
            /// <summary>
            /// GET method.
            /// </summary>
            public const string Get = "GET";
            /// <summary>
            /// HEAD method.
            /// </summary>
            public const string Head = "HEAD";
            /// <summary>
            /// OPTIONS method.
            /// </summary>
            public const string Options = "OPTIONS";
            /// <summary>
            /// PATCH method.
            /// </summary>
            public const string Patch = "PATCH";
            /// <summary>
            /// POST method.
            /// </summary>
            public const string Post = "POST";
            /// <summary>
            /// PUT method.
            /// </summary>
            public const string Put = "PUT";
            /// <summary>
            /// TRACE method.
            /// </summary>
            public const string Trace = "TRACE";
            /// <summary>
            /// Any HTTP method that the instrumentation has no prior knowledge of.
            /// </summary>
            public const string Other = "_OTHER";
        }
    }
}