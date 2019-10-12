// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace LoggingTracer.Demo.ConsoleApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var tracerFactory = new LoggingTracerFactory();
            var tracer = tracerFactory.GetTracer("ConsoleApp", "semver:1.0.0");

            using (tracer.WithSpan(tracer.StartSpan("Main (span1)")))
            {
                await Task.Delay(100);
                await Foo(tracer);
            }
        }

        private static async Task Foo(ITracer tracer)
        {
            using (tracer.WithSpan(tracer.StartSpan("Foo (span2)")))
            {
                tracer.CurrentSpan.SetAttribute("myattribute", "mvalue");
                await Task.Delay(100);
            }
        }
    }
}
