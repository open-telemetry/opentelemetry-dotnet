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
  Job-UQXYHB : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT

IterationCount=50  LaunchCount=10  WarmupCount=10

|          Method | Version |      Mean |    Error |    StdDev |    Median |   Gen 0 |   Gen 1 | Gen 2 | Allocated |
|---------------- |-------- |----------:|---------:|----------:|----------:|--------:|--------:|------:|----------:|
|     EncodeGauge |   0.4.0 |  90.38 us | 0.743 us |  4.922 us |  88.93 us | 20.9961 |  0.1221 |     - |  85.98 KB |
|     DecodeGauge |   0.4.0 |  80.52 us | 0.359 us |  2.367 us |  79.65 us | 30.2734 |  0.6104 |     - | 124.03 KB |
|   EncodeSummary |   0.4.0 | 120.16 us | 0.796 us |  5.251 us | 118.80 us | 29.0527 |  6.3477 |     - | 118.99 KB |
|   DecodeSummary |   0.4.0 | 224.80 us | 2.788 us | 18.662 us | 217.48 us | 49.0723 |  1.2207 |     - | 200.59 KB |
| EncodeHistogram |   0.4.0 | 166.26 us | 1.102 us |  7.261 us | 163.73 us | 41.0156 | 13.6719 |     - | 168.02 KB |
| DecodeHistogram |   0.4.0 | 277.14 us | 1.827 us | 11.845 us | 272.85 us | 74.2188 | 23.4375 |     - |  309.4 KB |
|     EncodeGauge |   0.5.0 |  93.26 us | 0.512 us |  3.351 us |  92.47 us | 19.1650 |  0.3662 |     - |  78.66 KB |
|     DecodeGauge |   0.5.0 |  81.69 us | 0.391 us |  2.530 us |  80.84 us | 28.4424 |       - |     - | 116.22 KB |
|   EncodeSummary |   0.5.0 |  95.05 us | 0.549 us |  3.596 us |  94.34 us | 19.1650 |  0.1221 |     - |  78.56 KB |
|   DecodeSummary |   0.5.0 |  90.39 us | 0.428 us |  2.757 us |  89.52 us | 30.5176 |  0.4883 |     - | 124.81 KB |
| EncodeHistogram |   0.5.0 | 154.30 us | 0.760 us |  4.806 us | 153.25 us | 36.1328 |  0.2441 |     - |  147.7 KB |
| DecodeHistogram |   0.5.0 | 260.91 us | 6.413 us | 41.713 us | 245.25 us | 60.5469 | 20.0195 |     - | 257.84 KB |
|     EncodeGauge |   0.6.0 |  92.14 us | 0.918 us |  5.845 us |  90.29 us | 16.3574 |  0.1221 |     - |  67.06 KB |
|     DecodeGauge |   0.6.0 | 100.51 us | 1.193 us |  7.711 us |  97.63 us | 29.1748 |  0.2441 |     - | 119.34 KB |
|   EncodeSummary |   0.6.0 |  89.41 us | 0.709 us |  4.612 us |  87.85 us | 16.6016 |       - |     - |  67.84 KB |
|   DecodeSummary |   0.6.0 |  91.23 us | 0.387 us |  2.566 us |  90.50 us | 28.4424 |       - |     - | 116.22 KB |
| EncodeHistogram |   0.6.0 | 166.32 us | 1.428 us |  9.412 us | 163.94 us | 36.8652 |  9.2773 |     - | 151.83 KB |
| DecodeHistogram |   0.6.0 | 279.38 us | 2.231 us | 14.414 us | 275.05 us | 70.8008 | 22.9492 |     - | 304.71 KB |
|     EncodeGauge |   0.7.0 |  97.39 us | 1.619 us | 10.779 us |  94.38 us | 16.3574 |  0.1221 |     - |  67.06 KB |
|     DecodeGauge |   0.7.0 |  99.56 us | 0.987 us |  6.559 us |  97.21 us | 29.0527 |  0.4883 |     - | 119.34 KB |
|   EncodeSummary |   0.7.0 | 122.87 us | 0.933 us |  6.127 us | 120.65 us | 23.6816 |  4.6387 |     - |  97.24 KB |
|   DecodeSummary |   0.7.0 | 235.73 us | 2.654 us | 17.616 us | 229.94 us | 46.3867 |  3.9063 |     - | 189.66 KB |
| EncodeHistogram |   0.7.0 | 165.69 us | 1.245 us |  8.091 us | 162.92 us | 36.8652 |  9.2773 |     - | 151.83 KB |
| DecodeHistogram |   0.7.0 | 273.84 us | 1.980 us | 12.905 us | 269.18 us | 68.8477 | 21.9727 |     - | 304.71 KB |
|     EncodeGauge |   0.8.0 |  92.84 us | 0.687 us |  4.558 us |  91.60 us | 17.0898 |  3.4180 |     - |  70.19 KB |
|     DecodeGauge |   0.8.0 |  94.21 us | 0.707 us |  4.688 us |  92.42 us | 29.7852 |  0.2441 |     - | 121.69 KB |
|   EncodeSummary |   0.8.0 | 127.31 us | 2.397 us | 15.724 us | 121.89 us | 23.8037 |  2.8076 |     - |  97.24 KB |
|   DecodeSummary |   0.8.0 | 238.60 us | 2.169 us | 14.517 us | 235.57 us | 46.3867 |  4.8828 |     - | 189.66 KB |
| EncodeHistogram |   0.8.0 | 176.01 us | 1.245 us |  8.271 us | 173.24 us | 41.0156 |  2.6855 |     - | 167.84 KB |
| DecodeHistogram |   0.8.0 | 290.50 us | 2.308 us | 15.303 us | 285.33 us | 68.3594 | 19.5313 |     - | 317.21 KB |
*/

namespace ProtoBench
{
    // [SimpleJob(launchCount: 10, warmupCount: 10, targetCount: 50)]
    [SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 1)]
    [MemoryDiagnoser]
    public class Benchmarks
    {
        private (string name, object value)[] resources;
        private (string name, string value)[] labels;
        private byte[] bytesGauge;
        private byte[] bytesSummary;
        private byte[] bytesHistogram;

        [Params("0.4.0", "0.5.0", "0.6.0", "0.7.0", "0.8.0")]
        public string Version { get; set; }

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
                    bytes = Otlp040.EncodeGauge(false, this.resources, this.labels, 1, 100, 1);
                    break;

                case "0.5.0":
                    bytes = Otlp050.EncodeGauge(false, this.resources, this.labels, 1, 100, 1);
                    break;

                case "0.6.0":
                    bytes = Otlp060.EncodeGauge(false, this.resources, this.labels, 1, 100, 1);
                    break;

                case "0.7.0":
                    bytes = Otlp070.EncodeGauge(false, this.resources, this.labels, 1, 100, 1);
                    break;

                case "0.8.0":
                    bytes = Otlp080.EncodeGauge(false, this.resources, this.labels, 1, 100, 1);
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
                    bytes = Otlp040.EncodeSummary(this.resources, this.labels, 1, 100, 1, 4);
                    break;

                case "0.5.0":
                    bytes = Otlp050.EncodeSummary(this.resources, this.labels, 1, 100, 1, 4);
                    break;

                case "0.7.0":
                    bytes = Otlp070.EncodeSummary(this.resources, this.labels, 1, 100, 1, 4);
                    break;

                case "0.6.0":
                    bytes = Otlp060.EncodeSummary(this.resources, this.labels, 1, 100, 1, 4);
                    break;

                case "0.8.0":
                    bytes = Otlp080.EncodeSummary(this.resources, this.labels, 1, 100, 1, 4);
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
                    bytes = Otlp040.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 4, 1);
                    break;

                case "0.5.0":
                    bytes = Otlp050.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 4, 1);
                    break;

                case "0.7.0":
                    bytes = Otlp070.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 4, 1);
                    break;

                case "0.6.0":
                    bytes = Otlp060.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 4, 1);
                    break;

                case "0.8.0":
                    bytes = Otlp080.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 4, 1);
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
