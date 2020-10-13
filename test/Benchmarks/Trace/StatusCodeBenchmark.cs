// <copyright file="StatusCodeBenchmark.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class StatusCodeBenchmark
    {
        private static readonly HashSet<StatusCode> Hashes = new HashSet<StatusCode>
        {
            StatusCode.Ok, StatusCode.Error, StatusCode.Unset,
        };

        [Benchmark]
        public void HashSetCheck()
        {
            object obj = 1;
            Hashes.Contains((StatusCode)obj);
        }

        [Benchmark]
        public void HashSetCheckDoesNotExist()
        {
            object obj = 100;
            Hashes.Contains((StatusCode)obj);
        }

        [Benchmark]
        public void EnumDefinedCheck()
        {
            object obj = 1;
            Enum.IsDefined(typeof(StatusCode), obj);
        }

        [Benchmark]
        public void EnumDefinedCheckDoesNotExist()
        {
            object obj = 100;
            Enum.IsDefined(typeof(StatusCode), obj);
        }
    }
}
