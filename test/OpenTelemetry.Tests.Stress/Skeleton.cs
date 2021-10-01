// <copyright file="Skeleton.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static volatile bool bContinue = true;
    private static volatile string output = "Test results not available yet.";

    public static void Stress(int concurrency = 0)
    {
#if DEBUG
        Console.WriteLine("***WARNING*** The current build is DEBUG which may affect timing!");
        Console.WriteLine();
#endif

        if (concurrency < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrency), "concurrency level should be a non-negative number.");
        }

        if (concurrency == 0)
        {
            concurrency = Environment.ProcessorCount;
        }

        var statistics = new long[concurrency];

        Parallel.Invoke(
            () =>
            {
                Console.WriteLine($"Running (concurrency = {concurrency}), press <Esc> to stop...");
                var bOutput = false;
                var watch = new Stopwatch();
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        switch (key)
                        {
                            case ConsoleKey.Enter:
                                Console.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToString("O"), output));
                                break;
                            case ConsoleKey.Escape:
                                bContinue = false;
                                return;
                            case ConsoleKey.Spacebar:
                                bOutput = !bOutput;
                                break;
                        }

                        continue;
                    }

                    if (bOutput)
                    {
                        Console.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToString("O"), output));
                    }

                    var cntLoopsOld = (ulong)statistics.Sum();
                    var cntCpuCyclesOld = GetCpuCycles();

                    watch.Restart();
                    Thread.Sleep(200);
                    watch.Stop();

                    var cntLoopsNew = (ulong)statistics.Sum();
                    var cntCpuCyclesNew = GetCpuCycles();

                    var nLoops = cntLoopsNew - cntLoopsOld;
                    var nCpuCycles = cntCpuCyclesNew - cntCpuCyclesOld;

                    var nLoopsPerSecond = (double)nLoops / ((double)watch.ElapsedMilliseconds / 1000.0);
                    var nCpuCyclesPerLoop = nLoops == 0 ? 0 : nCpuCycles / nLoops;

                    output = $"Loops: {cntLoopsNew:n0}, Loops/Second: {nLoopsPerSecond:n0}, CPU Cycles/Loop: {nCpuCyclesPerLoop:n0}";
                    Console.Title = output;
                }
            }, () =>
            {
                Parallel.For(0, concurrency, (i) =>
                {
                    statistics[i] = 0;
                    while (bContinue)
                    {
                        Run();
                        statistics[i]++;
                    }
                });
            });

        Console.WriteLine(output);
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryProcessCycleTime(IntPtr hProcess, out ulong cycles);

    private static ulong GetCpuCycles()
    {
#if NET462
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
        {
            return 0;
        }

        if (!QueryProcessCycleTime((IntPtr)(-1), out var cycles))
        {
            return 0;
        }

        return cycles;
    }
}
