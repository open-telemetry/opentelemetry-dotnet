// <copyright file="SqlClientInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.SqlClient
{
    /// <summary>
    /// Options for <see cref="SqlClientInstrumentation"/>.
    /// </summary>
    public class SqlClientInstrumentationOptions
    {
        /*
         * Match...
         *  serverName
         *  serverName[ ]\\[ ]instanceName
         *  serverName[ ],[ ]port
         *  serverName[ ]\\[ ]instanceName[ ],[ ]port
         * [ ] can be any number of white-space, SQL allows it for some reason.
         */
        private static readonly Regex DataSourceRegex = new Regex("^(.*?)\\s*(?:[\\\\,]|$)\\s*(.*?)\\s*(?:,|$)\\s*(.*)$", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<string, SqlConnectionDetails> ConnectionDetailCache = new ConcurrentDictionary<string, SqlConnectionDetails>(StringComparer.OrdinalIgnoreCase);

        // .NET Framework implementation uses SqlEventSource from which we can't reliably distinguish
        // StoredProcedures from regular Text sql commands.
#if NETFRAMEWORK

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientInstrumentation"/> should
        /// add the text of the executed Sql commands as the <see cref="SemanticConventions.AttributeDbStatement"/> tag.
        /// Default value: False.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WARNING: potential sensitive data capture! If you use <c>Microsoft.Data.SqlClient</c>, the instrumentation will capture <c>sqlCommand.CommandText</c>
        /// for <see cref="CommandType.StoredProcedure"/> and <see cref="CommandType.Text"/>. Make sure your <c>CommandText</c> property never contains
        /// any sensitive data for <see cref="CommandType.Text"/> commands.
        /// </para>
        /// <para>
        /// When using <c>System.Data.SqlClient</c>, the instrumentation will only capture <c>sqlCommand.CommandText</c> for <see cref="CommandType.StoredProcedure"/> commands.
        /// </para>
        /// </remarks>
        public bool SetStatementText { get; set; }
#else
        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientInstrumentation"/> should add the names of <see cref="CommandType.StoredProcedure"/> commands as the <see cref="SemanticConventions.AttributeDbStatement"/> tag. Default value: True.
        /// </summary>
        public bool SetStoredProcedureCommandName { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientInstrumentation"/> should add the text of <see cref="CommandType.Text"/> commands as the <see cref="SemanticConventions.AttributeDbStatement"/> tag. Default value: False.
        /// </summary>
        public bool SetTextCommandContent { get; set; }
#endif

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientInstrumentation"/> should parse the DataSource on a SqlConnection into server name, instance name, and/or port connection-level attribute tags. Default value: False.
        /// </summary>
        /// <remarks>
        /// The default behavior is to set the SqlConnection DataSource as the <see cref="SemanticConventions.AttributePeerService"/> tag. If enabled, SqlConnection DataSource will be parsed and the server name will be sent as the <see cref="SemanticConventions.AttributeNetPeerName"/> or <see cref="SemanticConventions.AttributeNetPeerIp"/> tag, the instance name will be sent as the <see cref="SemanticConventions.AttributeDbMsSqlInstanceName"/> tag, and the port will be sent as the <see cref="SemanticConventions.AttributeNetPeerPort"/> tag if it is not 1433 (the default port).
        /// </remarks>
        public bool EnableConnectionLevelAttributes { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity.
        /// </summary>
        /// <remarks>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para>string: the name of the event.</para>
        /// <para>object: the raw <c>SqlCommand</c> object from which additional information can be extracted to enrich the activity.</para>
        /// <para>See also: <a href="https://github.com/open-telemetry/opentelemetry-dotnet/tree/master/src/OpenTelemetry.Instrumentation.SqlClient#Enrich">example</a>.</para>
        /// </remarks>
        /// <example>
        /// <code>
        /// using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        ///     .AddSqlClientInstrumentation(opt => opt.Enrich
        ///         = (activity, eventName, rawObject) =>
        ///      {
        ///         if (eventName.Equals("OnCustom"))
        ///         {
        ///             if (rawObject is SqlCommand cmd)
        ///             {
        ///                 activity.SetTag("db.commandTimeout", cmd.CommandTimeout);
        ///             }
        ///         }
        ///      })
        ///     .Build();
        /// </code>
        /// </example>
        public Action<Activity, string, object> Enrich { get; set; }

        internal static SqlConnectionDetails ParseDataSource(string dataSource)
        {
            Match match = DataSourceRegex.Match(dataSource);

            string serverHostName = match.Groups[1].Value;
            string serverIpAddress = null;

            var uriHostNameType = Uri.CheckHostName(serverHostName);
            if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
            {
                serverIpAddress = serverHostName;
                serverHostName = null;
            }

            string instanceName;
            string port;
            if (match.Groups[3].Length > 0)
            {
                instanceName = match.Groups[2].Value;
                port = match.Groups[3].Value;
                if (port == "1433")
                {
                    port = null;
                }
            }
            else if (int.TryParse(match.Groups[2].Value, out int parsedPort))
            {
                port = parsedPort == 1433 ? null : match.Groups[2].Value;
                instanceName = null;
            }
            else
            {
                instanceName = match.Groups[2].Value;

                if (string.IsNullOrEmpty(instanceName))
                {
                    instanceName = null;
                }

                port = null;
            }

            return new SqlConnectionDetails
            {
                ServerHostName = serverHostName,
                ServerIpAddress = serverIpAddress,
                InstanceName = instanceName,
                Port = port,
            };
        }

        internal void AddConnectionLevelDetailsToActivity(string dataSource, Activity sqlActivity)
        {
            if (!this.EnableConnectionLevelAttributes)
            {
                sqlActivity.SetTag(SemanticConventions.AttributePeerService, dataSource);
            }
            else
            {
                if (!ConnectionDetailCache.TryGetValue(dataSource, out SqlConnectionDetails connectionDetails))
                {
                    connectionDetails = ParseDataSource(dataSource);
                    ConnectionDetailCache.TryAdd(dataSource, connectionDetails);
                }

                if (!string.IsNullOrEmpty(connectionDetails.ServerHostName))
                {
                    sqlActivity.SetTag(SemanticConventions.AttributeNetPeerName, connectionDetails.ServerHostName);
                }
                else
                {
                    sqlActivity.SetTag(SemanticConventions.AttributeNetPeerIp, connectionDetails.ServerIpAddress);
                }

                if (!string.IsNullOrEmpty(connectionDetails.InstanceName))
                {
                    sqlActivity.SetTag(SemanticConventions.AttributeDbMsSqlInstanceName, connectionDetails.InstanceName);
                }

                if (!string.IsNullOrEmpty(connectionDetails.Port))
                {
                    sqlActivity.SetTag(SemanticConventions.AttributeNetPeerPort, connectionDetails.Port);
                }
            }
        }

        internal class SqlConnectionDetails
        {
            public string ServerHostName { get; set; }

            public string ServerIpAddress { get; set; }

            public string InstanceName { get; set; }

            public string Port { get; set; }
        }
    }
}
