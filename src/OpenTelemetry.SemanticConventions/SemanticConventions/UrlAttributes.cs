// <copyright file="UrlAttributes.cs" company="OpenTelemetry Authors">
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
    /// Describes semantic conventions for attributes in the <c>url</c> namespace.
    /// </summary>
    public static class UrlAttributes
    {
        /// <summary>
        /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.5">URI fragment</a> component.
        /// </summary>
        public const string UrlFragment = "url.fragment";

        /// <summary>
        /// Absolute URL describing a network resource according to <a href="https://www.rfc-editor.org/rfc/rfc3986">RFC3986</a>.
        /// </summary>
        /// <remarks>
        /// For network calls, URL usually has <c>scheme://host[:port][path][?query][#fragment]</c> format, where the fragment is not transmitted over HTTP, but if it is known, it SHOULD be included nevertheless.
        /// <c>url.full</c> MUST NOT contain credentials passed via URL in form of <c>https://username:password@www.example.com/</c>. In such case username and password SHOULD be redacted and attribute&amp;#39;s value SHOULD be <c>https://REDACTED:REDACTED@www.example.com/</c>.
        /// <c>url.full</c> SHOULD capture the absolute URL when it is available (or can be reconstructed) and SHOULD NOT be validated or modified except for sanitizing purposes.
        /// </remarks>
        public const string UrlFull = "url.full";

        /// <summary>
        /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.3">URI path</a> component.
        /// </summary>
        public const string UrlPath = "url.path";

        /// <summary>
        /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.4">URI query</a> component.
        /// </summary>
        /// <remarks>
        /// Sensitive content provided in query string SHOULD be scrubbed when instrumentations can identify it.
        /// </remarks>
        public const string UrlQuery = "url.query";

        /// <summary>
        /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.1">URI scheme</a> component identifying the used protocol.
        /// </summary>
        public const string UrlScheme = "url.scheme";
    }
}