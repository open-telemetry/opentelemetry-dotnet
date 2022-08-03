// <copyright file="BatchLogRecordExportProcessorTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class BatchLogRecordExportProcessorTests
    {
        [Fact]
        public void StateValuesAndScopeBufferingTest()
        {
            var scopeProvider = new LoggerExternalScopeProvider();

            List<LogRecord> exportedItems = new();

            using var exporter = new BatchLogRecordExportProcessor(
                new InMemoryExporter<LogRecord>(exportedItems));

            using var scope = scopeProvider.Push(exportedItems);

            var logRecord = new LogRecord();

            var state = new LogRecordTest.DisposingState("Hello world");

            logRecord.ScopeProvider = scopeProvider;
            logRecord.StateValues = state;

            exporter.OnEnd(logRecord);

            state.Dispose();

            Assert.Empty(exportedItems);

            Assert.Null(logRecord.ScopeProvider);
            Assert.False(ReferenceEquals(state, logRecord.StateValues));
            Assert.NotNull(logRecord.AttributeStorage);
            Assert.NotNull(logRecord.BufferedScopes);

            KeyValuePair<string, object> actualState = logRecord.StateValues[0];

            Assert.Same("Value", actualState.Key);
            Assert.Same("Hello world", actualState.Value);

            bool foundScope = false;

            logRecord.ForEachScope<object>(
                (s, o) =>
                {
                    foundScope = ReferenceEquals(s.Scope, exportedItems);
                },
                null);

            Assert.True(foundScope);
        }

        [Fact]
        public void StateBufferingTest()
        {
            // LogRecord.State is never inspected or buffered. Accessing it
            // after OnEnd may throw. This test verifies that behavior. TODO:
            // Investigate this. Potentially obsolete logRecord.State and force
            // StateValues/ParseStateValues behavior.
            List<LogRecord> exportedItems = new();

            using var exporter = new BatchLogRecordExportProcessor(
                new InMemoryExporter<LogRecord>(exportedItems));

            var logRecord = new LogRecord();

            var state = new LogRecordTest.DisposingState("Hello world");
            logRecord.State = state;

            exporter.OnEnd(logRecord);

            state.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                IReadOnlyList<KeyValuePair<string, object>> state = (IReadOnlyList<KeyValuePair<string, object>>)logRecord.State;

                foreach (var kvp in state)
                {
                }
            });
        }
    }
}
#endif
