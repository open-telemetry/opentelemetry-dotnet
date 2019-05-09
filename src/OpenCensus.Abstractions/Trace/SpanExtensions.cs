// <copyright file="SpanExtensions.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    /// <summary>
    /// Helper class to populate well-known span attributes.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Helper method that populates span properties from http method according
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="method">Http method.</param>
        /// <returns>Span with populated http method properties.</returns>
        public static ISpan PutHttpMethodAttribute(this ISpan span, string method)
        {
            span.PutAttribute(SpanAttributeConstants.HttpMethodKey, AttributeValue.StringAttributeValue(method));
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http status code according
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="statusCode">Http status code.</param>
        /// <returns>Span with populated status code properties.</returns>
        public static ISpan PutHttpStatusCodeAttribute(this ISpan span, int statusCode)
        {
            span.PutAttribute(SpanAttributeConstants.HttpStatusCodeKey, AttributeValue.LongAttributeValue(statusCode));
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http user agent according
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="userAgent">Http status code.</param>
        /// <returns>Span with populated user agent code properties.</returns>
        public static ISpan PutHttpUserAgentAttribute(this ISpan span, string userAgent)
        {
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                span.PutAttribute(SpanAttributeConstants.HttpUserAgentKey, AttributeValue.StringAttributeValue(userAgent));
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from host and port
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="hostName">Hostr name.</param>
        /// <param name="port">Port number.</param>
        /// <returns>Span with populated host properties.</returns>
        public static ISpan PutHttpHostAttribute(this ISpan span, string hostName, int port)
        {
            if (port == 80 || port == 443)
            {
                span.PutAttribute(SpanAttributeConstants.HttpHostKey, AttributeValue.StringAttributeValue(hostName));
            }
            else
            {
                span.PutAttribute(SpanAttributeConstants.HttpHostKey, AttributeValue.StringAttributeValue(hostName + ":" + port));
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from route
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="route">Route used to resolve url to controller.</param>
        /// <returns>Span with populated route properties.</returns>
        public static ISpan PutHttpRouteAttribute(this ISpan span, string route)
        {
            if (!string.IsNullOrEmpty(route))
            {
                span.PutAttribute(SpanAttributeConstants.HttpRouteKey, AttributeValue.StringAttributeValue(route));
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from host and port
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="rawUrl">Raw url.</param>
        /// <returns>Span with populated url properties.</returns>
        public static ISpan PutHttpRawUrlAttribute(this ISpan span, string rawUrl)
        {
            if (!string.IsNullOrEmpty(rawUrl))
            {
                span.PutAttribute(SpanAttributeConstants.HttpUrlKey, AttributeValue.StringAttributeValue(rawUrl));
            }

            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from url path according
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="path">Url path.</param>
        /// <returns>Span with populated path properties.</returns>
        public static ISpan PutHttpPathAttribute(this ISpan span, string path)
        {
            span.PutAttribute(SpanAttributeConstants.HttpPathKey, AttributeValue.StringAttributeValue(path));
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from size according
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="size">Response size.</param>
        /// <returns>Span with populated response size properties.</returns>
        public static ISpan PutHttpResponseSizeAttribute(this ISpan span, long size)
        {
            span.PutAttribute(SpanAttributeConstants.HttpResponseSizeKey, AttributeValue.LongAttributeValue(size));
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from request size according
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="size">Request size.</param>
        /// <returns>Span with populated request size properties.</returns>
        public static ISpan PutHttpRequestSizeAttribute(this ISpan span, long size)
        {
            span.PutAttribute(SpanAttributeConstants.HttpRequestSizeKey, AttributeValue.LongAttributeValue(size));
            return span;
        }

        /// <summary>
        /// Helper method that populates span properties from http status code according
        /// to https://github.com/census-instrumentation/opencensus-specs/blob/4954074adf815f437534457331178194f6847ff9/trace/HTTP.md.
        /// </summary>
        /// <param name="span">Span to fill out.</param>
        /// <param name="statusCode">Http status code.</param>
        /// <param name="reasonPhrase">Http reason phrase.</param>
        /// <returns>Span with populated properties.</returns>
        public static ISpan PutHttpStatusCode(this ISpan span, int statusCode, string reasonPhrase)
        {
            span.PutHttpStatusCodeAttribute(statusCode);

            if ((int)statusCode < 200)
            {
                span.Status = Status.Unknown;
            }
            else if ((int)statusCode >= 200 && (int)statusCode <= 399)
            {
                span.Status = Status.Ok;
            }
            else if ((int)statusCode == 400)
            {
                span.Status = Status.InvalidArgument;
            }
            else if ((int)statusCode == 401)
            {
                span.Status = Status.Unauthenticated;
            }
            else if ((int)statusCode == 403)
            {
                span.Status = Status.PermissionDenied;
            }
            else if ((int)statusCode == 404)
            {
                span.Status = Status.NotFound;
            }
            else if ((int)statusCode == 429)
            {
                span.Status = Status.ResourceExhausted;
            }
            else if ((int)statusCode == 501)
            {
                span.Status = Status.Unimplemented;
            }
            else if ((int)statusCode == 503)
            {
                span.Status = Status.Unavailable;
            }
            else if ((int)statusCode == 504)
            {
                span.Status = Status.DeadlineExceeded;
            }
            else
            {
                span.Status = Status.Unknown;
            }

            span.Status = span.Status.WithDescription(reasonPhrase);

            return span;
        }
    }
}
