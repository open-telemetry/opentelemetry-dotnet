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

namespace OpenTelemetry.Instrumentation.Dependencies
{
    /// <summary>
    /// Options for <see cref="SqlClientInstrumentation"/>.
    /// </summary>
    public class SqlClientInstrumentationOptions
    {
        internal const string MicrosoftSqlServerDatabaseInstanceName = "db.mssql.instance_name";

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

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientInstrumentation"/> should capture the names of <see cref="CommandType.StoredProcedure"/> commands. Default value: True.
        /// </summary>
        public bool CaptureStoredProcedureCommandName { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientInstrumentation"/> should capture the text of <see cref="CommandType.Text"/> commands. Default value: False.
        /// </summary>
        public bool CaptureTextCommandContent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the <see cref="SqlClientInstrumentation"/> should parse the DataSource on a SqlConnection into server name, instance name, and/or port connection-level attributes. Default value: False.
        /// </summary>
        /// <remarks>
        /// The default behavior is to set the SqlConnection DataSource as the peer.service tag. If enabled, SqlConnection DataSource will be parsed and the server name will be sent as the net.peer.name or net.peer.ip tag, the instance name will be sent as the db.mssql.instance_name tag, and the port will be sent as the net.peer.port tag if it is not 1433 (the default port).
        /// </remarks>
        public bool EnableConnectionLevelAttributes { get; set; } = false;

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
                sqlActivity.AddTag(SemanticConventions.AttributePeerService, dataSource);
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
                    sqlActivity.AddTag(SemanticConventions.AttributeNetPeerName, connectionDetails.ServerHostName);
                }
                else
                {
                    sqlActivity.AddTag(SemanticConventions.AttributeNetPeerIP, connectionDetails.ServerIpAddress);
                }

                if (!string.IsNullOrEmpty(connectionDetails.InstanceName))
                {
                    sqlActivity.AddTag(MicrosoftSqlServerDatabaseInstanceName, connectionDetails.InstanceName);
                }

                if (!string.IsNullOrEmpty(connectionDetails.Port))
                {
                    sqlActivity.AddTag(SemanticConventions.AttributeNetPeerPort, connectionDetails.Port);
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
