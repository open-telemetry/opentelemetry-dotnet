// <copyright file="Benchmarks.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.201
  [Host]     : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT
  Job-ECBCNO : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT

IterationCount=20  LaunchCount=10  WarmupCount=4

|          Method | Version | IsDouble | NumMetrics | NumDataPoints |       Mean |    Error |    StdDev |     Median |    Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|---------------- |-------- |--------- |----------- |-------------- |-----------:|---------:|----------:|-----------:|---------:|---------:|--------:|-----------:|
|     EncodeGauge |   0.4.0 |    False |        100 |            10 |   590.2 us | 12.45 us |  50.35 us |   573.1 us |  66.4063 |  21.4844 |       - |  303.07 KB |
|     DecodeGauge |   0.4.0 |    False |        100 |            10 |   611.1 us | 11.05 us |  45.45 us |   600.0 us | 133.7891 |  58.5938 |       - |  746.52 KB |
|   EncodeSummary |   0.4.0 |    False |        100 |            10 |   947.9 us | 13.56 us |  54.52 us |   932.7 us | 102.5391 |  35.1563 | 34.1797 |  643.89 KB |
|   DecodeSummary |   0.4.0 |    False |        100 |            10 | 2,332.9 us | 36.61 us | 149.75 us | 2,292.6 us | 265.6250 | 132.8125 |       - | 1608.23 KB |
| EncodeHistogram |   0.4.0 |    False |        100 |            10 | 1,488.9 us | 25.51 us | 105.19 us | 1,466.0 us | 191.4063 |  93.7500 | 46.8750 | 1122.41 KB |
| DecodeHistogram |   0.4.0 |    False |        100 |            10 | 3,203.4 us | 50.48 us | 206.47 us | 3,150.5 us | 437.5000 | 218.7500 |       - | 2672.25 KB |
|     EncodeGauge |   0.8.0 |    False |        100 |            10 |   665.2 us | 19.82 us |  79.93 us |   643.9 us |  78.1250 |  21.4844 |       - |  368.41 KB |
|     DecodeGauge |   0.8.0 |    False |        100 |            10 |   613.4 us |  6.56 us |  26.23 us |   605.6 us | 145.5078 |  61.5234 |       - |  808.23 KB |
|   EncodeSummary |   0.8.0 |    False |        100 |            10 | 1,028.4 us | 15.80 us |  62.62 us | 1,012.2 us | 107.4219 |  70.3125 | 35.1563 |  638.91 KB |
|   DecodeSummary |   0.8.0 |    False |        100 |            10 | 2,329.2 us | 50.81 us | 207.79 us | 2,254.2 us | 261.7188 | 128.9063 |       - | 1596.51 KB |
| EncodeHistogram |   0.8.0 |    False |        100 |            10 | 1,714.9 us | 39.31 us | 162.11 us | 1,673.4 us | 201.1719 | 132.8125 | 66.4063 | 1336.11 KB |
| DecodeHistogram |   0.8.0 |    False |        100 |            10 | 3,461.5 us | 96.18 us | 389.01 us | 3,317.8 us | 468.7500 | 234.3750 |       - | 2848.81 KB |

Calculated % difference between 0.4.0 and 0.8.0

|          Method | Mean% | Allocated% |
|---------------- |------ |----------- |
|     EncodeGauge |   12% |        19% |
|     DecodeGauge |    0% |         8% |
|   EncodeSummary |    8% |        -1% |
|   DecodeSummary |    0% |        -1% |
| EncodeHistogram |   14% |        17% |
| DecodeHistogram |    8% |         6% |
*/

namespace ProtoBench
{
    // [SimpleJob(launchCount: 10, warmupCount: 4, targetCount: 20)]
    [SimpleJob(launchCount: 1, warmupCount: 4, targetCount: 20)]
    [MemoryDiagnoser]
    public class Benchmarks
    {
        private (string name, object value)[] resources;
        private (string name, string value)[] labels;
        private byte[] bytesGauge;
        private byte[] bytesSummary;
        private byte[] bytesHistogram;

        private int numLibs = 1;

        // [Params("0.4.0", "0.5.0", "0.6.0", "0.7.0", "0.8.0")]
        [Params("0.4.0", "0.8.0")]
        public string Version { get; set; }

        // [Params(true, false)]
        [Params(false)]
        public bool IsDouble { get; set; }

        [Params(100)]
        public int NumMetrics { get; set; }

        [Params(10)]
        public int NumDataPoints { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.resources = new (string name, object value)[]
            {
                ("StartTimeUnixnano", 12345678L),
                ("Pid", 1234L),
                ("HostName", "fakehost"),
                ("ServiceName", "generator"),
            };

            this.labels = new (string name, string value)[]
            {
                ("label1", "val1"),
                ("label2", "val2"),
            };

            this.bytesGauge = this.EncodeGauge();
            this.bytesSummary = this.EncodeSummary();
            this.bytesHistogram = this.EncodeHistogram();
        }

        [Benchmark]
        public byte[] EncodeGauge()
        {
            byte[] bytes = new byte[0];

            switch (this.Version)
            {
                case "0.4.0":
                    bytes = Otlp040.EncodeGauge(this.IsDouble, this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints);
                    break;

                case "0.5.0":
                    bytes = Otlp050.EncodeGauge(this.IsDouble, this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints);
                    break;

                case "0.6.0":
                    bytes = Otlp060.EncodeGauge(this.IsDouble, this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints);
                    break;

                case "0.7.0":
                    bytes = Otlp070.EncodeGauge(this.IsDouble, this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints);
                    break;

                case "0.8.0":
                    bytes = Otlp080.EncodeGauge(this.IsDouble, this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints);
                    break;
            }

            return bytes;
        }

        [Benchmark]
        public List<string> DecodeGauge()
        {
            List<string> extracts = new List<string>();

            switch (this.Version)
            {
                case "0.4.0":
                    extracts = Otlp040.Decode(this.bytesGauge);
                    break;

                case "0.5.0":
                    extracts = Otlp050.Decode(this.bytesGauge);
                    break;

                case "0.6.0":
                    extracts = Otlp060.Decode(this.bytesGauge);
                    break;

                case "0.7.0":
                    extracts = Otlp070.Decode(this.bytesGauge);
                    break;

                case "0.8.0":
                    extracts = Otlp080.Decode(this.bytesGauge);
                    break;
            }

            return extracts;
        }

        [Benchmark]
        public byte[] EncodeSummary()
        {
            byte[] bytes = new byte[0];

            switch (this.Version)
            {
                case "0.4.0":
                    bytes = Otlp040.EncodeSummary(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4);
                    break;

                case "0.5.0":
                    bytes = Otlp050.EncodeSummary(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4);
                    break;

                case "0.7.0":
                    bytes = Otlp070.EncodeSummary(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4);
                    break;

                case "0.6.0":
                    bytes = Otlp060.EncodeSummary(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4);
                    break;

                case "0.8.0":
                    bytes = Otlp080.EncodeSummary(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4);
                    break;
            }

            return bytes;
        }

        [Benchmark]
        public List<string> DecodeSummary()
        {
            List<string> extracts = new List<string>();

            switch (this.Version)
            {
                case "0.4.0":
                    extracts = Otlp040.Decode(this.bytesSummary);
                    break;

                case "0.5.0":
                    extracts = Otlp050.Decode(this.bytesSummary);
                    break;

                case "0.6.0":
                    extracts = Otlp060.Decode(this.bytesSummary);
                    break;

                case "0.7.0":
                    extracts = Otlp070.Decode(this.bytesSummary);
                    break;

                case "0.8.0":
                    extracts = Otlp080.Decode(this.bytesSummary);
                    break;
            }

            return extracts;
        }

        [Benchmark]
        public byte[] EncodeHistogram()
        {
            byte[] bytes = new byte[0];

            switch (this.Version)
            {
                case "0.4.0":
                    bytes = Otlp040.EncodeHistogram(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4, 1);
                    break;

                case "0.5.0":
                    bytes = Otlp050.EncodeHistogram(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4, 1);
                    break;

                case "0.7.0":
                    bytes = Otlp070.EncodeHistogram(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4, 1);
                    break;

                case "0.6.0":
                    bytes = Otlp060.EncodeHistogram(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4, 1);
                    break;

                case "0.8.0":
                    bytes = Otlp080.EncodeHistogram(this.resources, this.labels, this.numLibs, this.NumMetrics, this.NumDataPoints, 4, 1);
                    break;
            }

            return bytes;
        }

        [Benchmark]
        public List<string> DecodeHistogram()
        {
            List<string> extracts = new List<string>();

            switch (this.Version)
            {
                case "0.4.0":
                    extracts = Otlp040.Decode(this.bytesHistogram);
                    break;

                case "0.5.0":
                    extracts = Otlp050.Decode(this.bytesHistogram);
                    break;

                case "0.7.0":
                    extracts = Otlp070.Decode(this.bytesHistogram);
                    break;

                case "0.6.0":
                    extracts = Otlp060.Decode(this.bytesHistogram);
                    break;

                case "0.8.0":
                    extracts = Otlp080.Decode(this.bytesHistogram);
                    break;
            }

            return extracts;
        }
    }
}
