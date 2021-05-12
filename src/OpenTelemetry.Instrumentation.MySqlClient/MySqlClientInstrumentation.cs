// <copyright file="MySqlClientInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

using MySql.Data.MySqlClient;

using OpenTelemetry.Instrumentation.MySqlClient.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.MySqlClient
{
    /// <summary>
    /// Mysql.Data instrumentation.
    /// </summary>
    internal class MySqlClientInstrumentation : DefaultTraceListener
    {
        private readonly ConcurrentDictionary<long, MySqlConnectionStringBuilder> dbConn = new ConcurrentDictionary<long, MySqlConnectionStringBuilder>();

        private readonly MySqlClientInstrumentationOptions options;

        public MySqlClientInstrumentation(MySqlClientInstrumentationOptions options = null)
        {
            this.options = options ?? new MySqlClientInstrumentationOptions();
            MySqlTrace.Listeners.Clear();
            MySqlTrace.Listeners.Add(this);
            MySqlTrace.Switch.Level = SourceLevels.Information;
            MySqlTrace.QueryAnalysisEnabled = true;
        }

        /// <inheritdoc />
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            switch ((MySqlTraceEventType)id)
            {
                case MySqlTraceEventType.ConnectionOpened:
                    // args: [driverId, connStr, threadId]
                    var driverId = (long)args[0];
                    var connStr = args[1].ToString();
                    this.dbConn[driverId] = new MySqlConnectionStringBuilder(connStr);
                    break;
                case MySqlTraceEventType.ConnectionClosed:
                    break;
                case MySqlTraceEventType.QueryOpened:
                    // args: [driverId, threadId, cmdText]
                    this.BeforeExecuteCommand(this.GetCommand(args[0], args[2]));
                    break;
                case MySqlTraceEventType.ResultOpened:
                    break;
                case MySqlTraceEventType.ResultClosed:
                    break;
                case MySqlTraceEventType.QueryClosed:
                    // args: [driverId]
                    this.AfterExecuteCommand();
                    break;
                case MySqlTraceEventType.StatementPrepared:
                    break;
                case MySqlTraceEventType.StatementExecuted:
                    break;
                case MySqlTraceEventType.StatementClosed:
                    break;
                case MySqlTraceEventType.NonQuery:
                    break;
                case MySqlTraceEventType.UsageAdvisorWarning:
                    break;
                case MySqlTraceEventType.Warning:
                    break;
                case MySqlTraceEventType.Error:
                    // args: [driverId, exNumber, exMessage]
                    this.ErrorExecuteCommand(this.GetMySqlErrorException(args[2]));
                    break;
                case MySqlTraceEventType.QueryNormalized:
                    break;
            }
        }

        private void BeforeExecuteCommand(MySqlDataTraceCommand command)
        {
            var activity = MySqlActivitySourceHelper.ActivitySource.StartActivity(
                MySqlActivitySourceHelper.ActivityName,
                ActivityKind.Client,
                Activity.Current?.Context ?? default(ActivityContext),
                MySqlActivitySourceHelper.CreationTags);
            if (activity == null)
            {
                return;
            }

            if (activity.IsAllDataRequested)
            {
                if (this.options.SetDbStatement)
                {
                    activity.SetTag(SemanticConventions.AttributeDbStatement, command.SqlText);
                }

                if (command.ConnectionStringBuilder != null)
                {
                    activity.DisplayName = command.ConnectionStringBuilder.Database;
                    activity.SetTag(SemanticConventions.AttributeDbName, command.ConnectionStringBuilder.Database);

                    this.AddConnectionLevelDetailsToActivity(command.ConnectionStringBuilder, activity);
                }
            }
        }

        private void AfterExecuteCommand()
        {
            var activity = Activity.Current;
            if (activity == null)
            {
                return;
            }

            if (activity.Source != MySqlActivitySourceHelper.ActivitySource)
            {
                return;
            }

            try
            {
                if (activity.IsAllDataRequested)
                {
                    activity.SetStatus(Status.Unset);
                }
            }
            finally
            {
                activity.Stop();
            }
        }

        private void ErrorExecuteCommand(Exception exception)
        {
            var activity = Activity.Current;
            if (activity == null)
            {
                return;
            }

            if (activity.Source != MySqlActivitySourceHelper.ActivitySource)
            {
                return;
            }

            try
            {
                if (activity.IsAllDataRequested)
                {
                    activity.SetStatus(Status.Error.WithDescription(exception.Message));
                    if (this.options.RecordException)
                    {
                        activity.RecordException(exception);
                    }
                }
            }
            finally
            {
                activity.Stop();
            }
        }

        private MySqlDataTraceCommand GetCommand(object driverIdObj, object cmd)
        {
            var command = new MySqlDataTraceCommand();
            if (this.dbConn.TryGetValue((long)driverIdObj, out var database))
            {
                command.ConnectionStringBuilder = database;
            }

            command.SqlText = cmd == null ? string.Empty : cmd.ToString();
            return command;
        }

        private Exception GetMySqlErrorException(object errorMsg)
        {
            return new Exception($"{errorMsg}");
        }

        private void AddConnectionLevelDetailsToActivity(MySqlConnectionStringBuilder dataSource, Activity sqlActivity)
        {
            if (!this.options.EnableConnectionLevelAttributes)
            {
                sqlActivity.SetTag(SemanticConventions.AttributePeerService, dataSource.Server);
            }
            else
            {
                var uriHostNameType = Uri.CheckHostName(dataSource.Server);

                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    sqlActivity.SetTag(SemanticConventions.AttributeNetPeerIp, dataSource.Server);
                }
                else
                {
                    sqlActivity.SetTag(SemanticConventions.AttributeNetPeerName, dataSource.Server);
                }

                sqlActivity.SetTag(SemanticConventions.AttributeNetPeerPort, dataSource.Port);
                sqlActivity.SetTag(SemanticConventions.AttributeDbUser, dataSource.UserID);
            }
        }
    }
}
