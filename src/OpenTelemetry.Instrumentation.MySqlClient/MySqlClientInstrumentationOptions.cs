// <copyright file="MySqlClientInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MySqlClient
{
    /// <summary>
    /// Options for <see cref="MySqlClientInstrumentation"/>.
    /// </summary>
    public class MySqlClientInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the exception will be recorded as ActivityEvent or not. Default value: False.
        /// </summary>
        /// <remarks>
        /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md.
        /// </remarks>
        public bool RecordException { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="MySqlClientInstrumentation"/> should add the text as the <see cref="SemanticConventions.AttributeDbStatement"/> tag. Default value: False.
        /// </summary>
        public bool SetDbStatement { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="MySqlClientInstrumentation"/> should parse the DataSource on a SqlConnection into server name, instance name, and/or port connection-level attribute tags. Default value: False.
        /// </summary>
        /// <remarks>
        /// The default behavior is to set the MySqlConnection DataSource as the <see cref="SemanticConventions.AttributePeerService"/> tag. If enabled, MySqlConnection DataSource will be parsed and the server name will be sent as the <see cref="SemanticConventions.AttributeNetPeerName"/> or <see cref="SemanticConventions.AttributeNetPeerIp"/> tag, the instance name will be sent as the <see cref="SemanticConventions.AttributeDbMsSqlInstanceName"/> tag, and the port will be sent as the <see cref="SemanticConventions.AttributeNetPeerPort"/> tag if it is not 1433 (the default port).
        /// </remarks>
        public bool EnableConnectionLevelAttributes { get; set; }
    }
}
