// <copyright file="MyLibrary.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics;

#pragma warning disable CS0618

namespace HttpServerExample
{
    public class MyLibrary
    {
        Random rand = new Random();

        public MyLibrary()
        {
            Meter meter = MeterProvider.Default.GetMeter("MyLibrary", "1.0.0");

            var hostLabels = new MyLabelSet(("Host Name", GetHostName()));

            meter.CreateDoubleObserver("ServerRoomTemp", (k) => 
                {
                    double temp = GetServerRoomTemperature();
                    k.Observe(temp, hostLabels);
                },
                true);

            meter.CreateDoubleObserver("SystemCpuUsage", (k) => 
                {
                    int cpu = GetSystemCpu();
                    k.Observe(cpu, hostLabels);
                },
                true);

            meter.CreateDoubleObserver("ProcessCpuUsage", (k) => 
                {
                    int cpu = GetSystemCpu();
                    int pid = GetProcessCpu();

                    var processLabels = new MyLabelSet(
                        ("Host Name", GetHostName()),
                        ("Process Id", $"{GetProcessId()}")
                    );
                    k.Observe(cpu, processLabels);
                },
                true);
        }

        public void Shutdown()
        {
            // Shutdown
        }

        public string GetHostName()
        {
            return "MachineA";
        }

        public int GetProcessId()
        {
            var r = rand.Next(10);
            return 1230 + r;
        }

        public double GetServerRoomTemperature()
        {
            var r = rand.Next(10);
            return 70.2 + r;
        }

        public int GetSystemCpu()
        {
            var r = rand.Next(20);
            return 30 + r;
        }

        public int GetProcessCpu()
        {
            var r = rand.Next(20);
            return 10 + r;
        }
    }
}
