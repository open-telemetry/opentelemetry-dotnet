// <copyright file="SqlClientDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector.Dependencies.Implementation
{
    internal class SqlClientDiagnosticListener : ListenerHandler
    {
        private readonly PropertyFetcher commandFetcher = new PropertyFetcher("Command");
        private readonly PropertyFetcher connectionFetcher = new PropertyFetcher("Connection");
        private readonly PropertyFetcher dataSourceFetcher = new PropertyFetcher("DataSource");
        private readonly PropertyFetcher databaseFetcher = new PropertyFetcher("Database");
        private readonly PropertyFetcher commandTextFetcher = new PropertyFetcher("CommandText");
        private readonly PropertyFetcher exceptionFetcher = new PropertyFetcher("Exception");

        public SqlClientDiagnosticListener(string sourceName, ITracer tracer)
            : base(sourceName, tracer)
        {
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            switch (name)
            {
                case "System.Data.SqlClient.WriteCommandBefore":
                case "Microsoft.Data.SqlClient.WriteCommandBefore":
                    {
                        var command = this.commandFetcher.Fetch(payload);

                        if (command == null)
                        {
                            CollectorEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener) + name);
                            return;
                        }

                        var connection = this.connectionFetcher.Fetch(command);
                        var dataSource = this.dataSourceFetcher.Fetch(connection);
                        var database = this.databaseFetcher.Fetch(connection);
                        var commandText = this.commandTextFetcher.Fetch(command);

                        this.Tracer.StartActiveSpan(database.ToString(), SpanKind.Client, out var span);

                        span.PutComponentAttribute("SQL Server");

                        span.PutDatabaseTypeAttribute("sql");
                        span.PutDatabaseInstanceAttribute(database.ToString());
                        span.PutDatabaseStatementAttribute(commandText.ToString());
                        span.SetAttribute("peer.address", dataSource.ToString());
                    }

                    break;
                case "System.Data.SqlClient.WriteCommandAfter":
                case "Microsoft.Data.SqlClient.WriteCommandAfter":
                    {
                        var span = this.Tracer.CurrentSpan;

                        if (span == null || span == BlankSpan.Instance)
                        {
                            CollectorEventSource.Log.NullOrBlankSpan(nameof(SqlClientDiagnosticListener) + name);
                            return;
                        }

                        span.End();
                    }

                    break;
                case "System.Data.SqlClient.WriteCommandError":
                case "Microsoft.Data.SqlClient.WriteCommandError":
                    {
                        var span = this.Tracer.CurrentSpan;

                        if (span == null || span == BlankSpan.Instance)
                        {
                            CollectorEventSource.Log.NullOrBlankSpan(nameof(SqlClientDiagnosticListener) + name);
                            return;
                        }

                        if (span.IsRecording)
                        {
                            if (!(this.exceptionFetcher.Fetch(payload) is Exception exception))
                            {
                                CollectorEventSource.Log.NullPayload(nameof(SqlClientDiagnosticListener) + name);
                                return;
                            }

                            span.Status = Status.Unknown.WithDescription(exception.Message);
                        }
                    }

                    break;
            }
        }
    }
}
