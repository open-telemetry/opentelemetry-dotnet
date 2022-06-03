// <copyright file="SqlClientMetricsDiagnosticListener.cs" company="OpenTelemetry Authors">
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
#if !NETFRAMEWORK
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTelemetry.Trace;
using static OpenTelemetry.Instrumentation.SqlClient.SqlClientInstrumentationOptions;

namespace OpenTelemetry.Instrumentation.SqlClient.Implementation
{
    internal sealed class SqlClientMetricsDiagnosticListener : ListenerHandler
    {
        public const string SqlDataBeforeExecuteCommand = "System.Data.SqlClient.WriteCommandBefore";
        public const string SqlMicrosoftBeforeExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandBefore";

        public const string SqlDataAfterExecuteCommand = "System.Data.SqlClient.WriteCommandAfter";
        public const string SqlMicrosoftAfterExecuteCommand = "Microsoft.Data.SqlClient.WriteCommandAfter";

        public const string SqlDataWriteCommandError = "System.Data.SqlClient.WriteCommandError";
        public const string SqlMicrosoftWriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";

        private static readonly ConcurrentDictionary<string, SqlConnectionDetails> ConnectionDetailCache = new(StringComparer.OrdinalIgnoreCase);
        private static AsyncLocal<DateTime> asyncLocalStartTime = new AsyncLocal<DateTime>();

        private readonly PropertyFetcher<object> afterExecuteCommandFetcher = new("Command");
        private readonly PropertyFetcher<object> connectionFetcher = new("Connection");
        private readonly PropertyFetcher<object> dataSourceFetcher = new("DataSource");

        private readonly Meter meter;
        private readonly Histogram<double> dbCallDuration;

        public SqlClientMetricsDiagnosticListener(string name, Meter meter)
           : base(name)
        {
            this.meter = meter;
            this.dbCallDuration = meter.CreateHistogram<double>("db.sqlcommand.duration", "ms", "measures the command execution duration of the outbound db call");
        }

        public override bool SupportsNullActivity => true;

        public override void OnCustom(string name, Activity activity, object payload)
        {
            switch (name)
            {
                case SqlDataBeforeExecuteCommand:
                case SqlMicrosoftBeforeExecuteCommand:
                    {
                        asyncLocalStartTime.Value = DateTime.UtcNow;
                    }

                    break;
                case SqlDataAfterExecuteCommand:
                case SqlMicrosoftAfterExecuteCommand:
                    {
                        _ = this.afterExecuteCommandFetcher.TryFetch(payload, out var command);
                        _ = this.connectionFetcher.TryFetch(command, out var connection);
                        _ = this.dataSourceFetcher.TryFetch(connection, out var dataSource);

                        var dataSourceString = (string)dataSource;
                        if (!ConnectionDetailCache.TryGetValue(dataSourceString, out SqlConnectionDetails connectionDetails))
                        {
                            connectionDetails = ParseDataSource(dataSourceString);
                            ConnectionDetailCache.TryAdd(dataSourceString, connectionDetails);
                        }

                        // TODO: Add null check for tags and more tags
                        var tags = new TagList
                        {
                            { SemanticConventions.AttributeNetPeerName, connectionDetails.ServerHostName },
                        };

                        var duration = (DateTime.UtcNow - asyncLocalStartTime.Value).TotalMilliseconds;

                        Console.WriteLine("asynclocal duration: " + duration);

                        this.dbCallDuration.Record(duration, tags);
                    }

                    break;
                case SqlDataWriteCommandError:
                case SqlMicrosoftWriteCommandError:
                    break;
            }
        }
    }
}
#endif
