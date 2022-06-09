// <copyright file="PrometheusExporterHttpServerOptions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// <see cref="PrometheusExporterHttpServer"/> options.
    /// </summary>
    public class PrometheusExporterHttpServerOptions
    {
        private IReadOnlyCollection<string> httpListenerPrefixes = new string[] { "http://localhost:9464/" };

#if NETCOREAPP3_1_OR_GREATER
        /// <summary>
        /// Gets or sets a value indicating whether or not an http listener
        /// should be started. Default value: False.
        /// </summary>
        public bool StartHttpListener { get; set; }
#else
        /// <summary>
        /// Gets or sets a value indicating whether or not an http listener
        /// should be started. Default value: True.
        /// </summary>
        public bool StartHttpListener { get; set; } = true;
#endif

        /// <summary>
        /// Gets or sets the prefixes to use for the http listener. Default
        /// value: http://localhost:9464/.
        /// </summary>
        public IReadOnlyCollection<string> HttpListenerPrefixes
        {
            get => this.httpListenerPrefixes;
            set
            {
                Guard.ThrowIfNull(value);

                foreach (string inputUri in value)
                {
                    if (!(Uri.TryCreate(inputUri, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
                    {
                        throw new ArgumentException(
                            "Prometheus server path should be a valid URI with http/https scheme.",
                            nameof(this.httpListenerPrefixes));
                    }
                }

                this.httpListenerPrefixes = value;
            }
        }
    }
}
