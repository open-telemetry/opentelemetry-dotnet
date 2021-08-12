// <copyright file="ThreadSignalingBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class ThreadSignalingBenchmarks
    {
        private readonly ManualResetEvent manualResetEvent;
        private readonly ManualResetEventSlim manualResetEventSlim;

        public ThreadSignalingBenchmarks()
        {
            this.manualResetEvent = new ManualResetEvent(false);
            this.manualResetEventSlim = new ManualResetEventSlim(false);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.manualResetEvent?.Dispose();
            this.manualResetEventSlim?.Dispose();
        }

        [Benchmark]
        public void ManualResetEventWaitHandle()
        {
            WaitHandle waitHandle = this.manualResetEvent;
        }

        [Benchmark]
        public void ManualResetEventSlimWaitHandle()
        {
            WaitHandle waitHandle = this.manualResetEventSlim.WaitHandle;
        }

        [Benchmark]
        public void ManualResetEventSet()
        {
            this.manualResetEvent.Set();
        }

        [Benchmark]
        public void ManualResetEventSlimSet()
        {
            this.manualResetEventSlim.Set();
        }

        [Benchmark]
        public void ManualResetEventSetReset()
        {
            this.manualResetEvent.Set();
            this.manualResetEvent.Reset();
        }

        [Benchmark]
        public void ManualResetEventSlimSetReset()
        {
            this.manualResetEventSlim.Set();
            this.manualResetEventSlim.Reset();
        }
    }
}
