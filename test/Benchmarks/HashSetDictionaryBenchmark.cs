// <copyright file="HashSetDictionaryBenchmark.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Logs
{
    [MemoryDiagnoser]
    public class HashSetDictionaryBenchmark
    {
        private static Random random = new Random();
        private static string[] randomStringArray = RandomStringGenerator(100000);
        private readonly int halfpoint = 100000 / 2;

        [Benchmark]
        public void HashSet()
        {
            var testSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < this.halfpoint; ++i)
            {
                testSet.Add(randomStringArray[i]);
            }

            for (int i = this.halfpoint; i < 100000; ++i)
            {
                testSet.Contains(randomStringArray[i]);
            }
        }

        [Benchmark]
        public void Dictionary()
        {
            var testDict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < this.halfpoint; ++i)
            {
                testDict[randomStringArray[i]] = true;
            }

            for (int i = this.halfpoint; i < 100000; ++i)
            {
                testDict.ContainsKey(randomStringArray[i]);
            }
        }

        private static string[] RandomStringGenerator(int number)
        {
            string[] testStringArr = new string[number];
            const string chars = "ABCDEFGHIJKLMnopqrstuvwxyz";
            for (int i = 0; i < number; ++i)
            {
                var curStr = new string(Enumerable.Repeat(chars, 3)
                .Select(s => s[random.Next(s.Length)]).ToArray());
                testStringArr[i] = curStr;
            }

            return testStringArr;
        }
    }
}
