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
using System.Data.SqlClient;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector.Dependencies.Implementation
{
    internal class SqlClientDiagnosticListener : ListenerHandler
    {
        private readonly PropertyFetcher commandFetcher = new PropertyFetcher("Command");
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
                    {
                        this.Tracer.StartActiveSpan("Execute SQL", SpanKind.Client, out var span);

                        if (this.commandFetcher.Fetch(payload) is SqlCommand command && span.IsRecording)
                        {
                            span.PutDatabaseTypeAttribute("sql");
                            span.PutDatabaseInstanceAttribute(command.Connection.Database);
                            span.PutDatabaseStatementAttribute(command.CommandText);
                        }
                    }

                    break;
                case "System.Data.SqlClient.WriteCommandError":
                    {
                        if (this.exceptionFetcher.Fetch(payload) is Exception exception)
                        {
                            this.Tracer.CurrentSpan.SetAttribute("error", exception.Message);
                        }

                        this.Tracer.CurrentSpan.End();
                    }

                    break;
                case "System.Data.SqlClient.WriteCommandAfter":
                    {
                        this.Tracer.CurrentSpan.End();
                    }

                    break;
            }
        }
    }
}
