﻿// <copyright file="SqlClientDiagnosticListener.cs" company="OpenTelemetry Authors">
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
using System.Data;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector.Dependencies.Implementation
{
    internal class SqlClientDiagnosticListener : ListenerHandler
    {
        internal const string SqlDataBeforeExecuteCommand = "System.Data.SqlClient.WriteCommandBefore";
        internal const string SqlMicrosoftBeforeExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandBefore";

        internal const string SqlDataAfterExecuteCommand = "System.Data.SqlClient.WriteCommandAfter";
        internal const string SqlMicrosoftAfterExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandAfter";

        internal const string SqlDataWriteCommandError = "System.Data.SqlClient.WriteCommandError";
        internal const string SqlMicrosoftWriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";

        private readonly PropertyFetcher commandFetcher = new PropertyFetcher("Command");
        private readonly PropertyFetcher connectionFetcher = new PropertyFetcher("Connection");
        private readonly PropertyFetcher dataSourceFetcher = new PropertyFetcher("DataSource");
        private readonly PropertyFetcher databaseFetcher = new PropertyFetcher("Database");
        private readonly PropertyFetcher commandTypeFetcher = new PropertyFetcher("CommandType");
        private readonly PropertyFetcher commandTextFetcher = new PropertyFetcher("CommandText");
        private readonly PropertyFetcher exceptionFetcher = new PropertyFetcher("Exception");
        private readonly SqlClientCollectorOptions options;

        public SqlClientDiagnosticListener(string sourceName, Tracer tracer, SqlClientCollectorOptions options)
            : base(sourceName, tracer)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            switch (name)
            {
                case SqlDataBeforeExecuteCommand:
                case SqlMicrosoftBeforeExecuteCommand:
                    {
                        var command = this.commandFetcher.Fetch(payload);

                        if (command == null)
                        {
                            CollectorEventSource.Log.NullPayload($"{nameof(SqlClientDiagnosticListener)}-{name}");
                            return;
                        }

                        var connection = this.connectionFetcher.Fetch(command);
                        var database = this.databaseFetcher.Fetch(connection);

                        this.Tracer.StartActiveSpan((string)database, SpanKind.Client, out var span);

                        if (span.IsRecording)
                        {
                            var dataSource = this.dataSourceFetcher.Fetch(connection);
                            var commandText = this.commandTextFetcher.Fetch(command);

                            span.PutComponentAttribute("sql");

                            span.PutDatabaseTypeAttribute("sql");
                            span.PutPeerServiceAttribute((string)dataSource);
                            span.PutDatabaseInstanceAttribute((string)database);

                            if (this.commandTypeFetcher.Fetch(command) is CommandType commandType)
                            {
                                span.SetAttribute("db.statementType", commandType.ToString());

                                switch (commandType)
                                {
                                    case CommandType.StoredProcedure:
                                        if (this.options.CaptureStoredProcedureCommandContent)
                                        {
                                            span.PutDatabaseStatementAttribute((string)commandText);
                                        }

                                        break;

                                    case CommandType.Text:
                                        if (this.options.CaptureTextCommandContent)
                                        {
                                            span.PutDatabaseStatementAttribute((string)commandText);
                                        }

                                        break;
                                }
                            }
                        }
                    }

                    break;
                case SqlDataAfterExecuteCommand:
                case SqlMicrosoftAfterExecuteCommand:
                    {
                        var span = this.Tracer.CurrentSpan;

                        if (span == null || !span.Context.IsValid)
                        {
                            CollectorEventSource.Log.NullOrBlankSpan($"{nameof(SqlClientDiagnosticListener)}-{name}");
                            return;
                        }

                        span.End();
                    }

                    break;
                case SqlDataWriteCommandError:
                case SqlMicrosoftWriteCommandError:
                    {
                        var span = this.Tracer.CurrentSpan;

                        if (span == null || !span.Context.IsValid)
                        {
                            CollectorEventSource.Log.NullOrBlankSpan($"{nameof(SqlClientDiagnosticListener)}-{name}");
                            return;
                        }

                        if (span.IsRecording)
                        {
                            if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                            {
                                span.Status = Status.Unknown.WithDescription(exception.Message);
                            }
                            else
                            {
                                CollectorEventSource.Log.NullPayload($"{nameof(SqlClientDiagnosticListener)}-{name}");
                            }
                        }
                    }

                    break;
            }
        }
    }
}
