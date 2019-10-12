// <copyright file="Logger.cs" company="OpenTelemetry Authors">
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
using System.Threading;

namespace LoggingTracer
{
    public static class Logger
    {
        private static readonly DateTime StartTime = DateTime.UtcNow;

        static Logger() => PrintHeader();

        public static void PrintHeader()
        {
            Console.WriteLine("MsSinceStart | ThreadId | API");
        }

        public static void Log(string s)
        {
            Console.WriteLine($"{MillisSinceStart(),12} | {Thread.CurrentThread.ManagedThreadId,8} | {s}");
        }

        private static int MillisSinceStart()
        {
            return (int)DateTime.UtcNow.Subtract(StartTime).TotalMilliseconds;
        }
    }
}
