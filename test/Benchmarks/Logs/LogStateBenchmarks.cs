// <copyright file="LogStateBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Benchmarks.Logs
{
    [MemoryDiagnoser]
    public class LogStateBenchmarks
    {
        private IReadOnlyList<KeyValuePair<string, object>> listStateValues;
        private IReadOnlyList<KeyValuePair<string, object>> readonlyStateValues;
        private OpenTelemetryLogger.LoggerStateCopy copiedStateValues;

        [Params(0, 100)]
        public int NumberOfItemsInState { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (this.NumberOfItemsInState > 0)
            {
                var stateValues = new List<KeyValuePair<string, object>>(this.NumberOfItemsInState);
                for (int i = 0; i < this.NumberOfItemsInState; i++)
                {
                    stateValues.Add(new KeyValuePair<string, object>("Key", null));
                }

                this.listStateValues = stateValues;

                this.readonlyStateValues = new CustomReadOnlyListState(this.NumberOfItemsInState);

                this.copiedStateValues = new OpenTelemetryLogger.LoggerStateCopy(stateValues);
            }
        }

        [Benchmark]
        public void BufferListState()
        {
            var logRecord = new LogRecord(
                null,
                DateTime.UtcNow,
                "Benchmark",
                LogLevel.Information,
                0,
                "Message",
                null,
                null,
                this.listStateValues);

            logRecord.Buffer();
        }

        [Benchmark]
        public void BufferReadOnlyState()
        {
            var logRecord = new LogRecord(
                null,
                DateTime.UtcNow,
                "Benchmark",
                LogLevel.Information,
                0,
                "Message",
                null,
                null,
                this.readonlyStateValues);

            logRecord.Buffer();
        }

        [Benchmark]
        public void BufferCopiedState()
        {
            var logRecord = new LogRecord(
                null,
                DateTime.UtcNow,
                "Benchmark",
                LogLevel.Information,
                0,
                "Message",
                null,
                null,
                this.copiedStateValues);

            logRecord.Buffer();
        }

        private sealed class CustomReadOnlyListState : IReadOnlyList<KeyValuePair<string, object>>
        {
            private readonly int numberOfItems;

            public CustomReadOnlyListState(int numberOfItems)
            {
                this.numberOfItems = numberOfItems;
            }

            public int Count => this.numberOfItems;

            public KeyValuePair<string, object> this[int index]
                => new("Key", null);

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                for (var i = 0; i < this.Count; i++)
                {
                    yield return this[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}
