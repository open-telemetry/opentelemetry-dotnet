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
  Job-SYFRJT : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT

IterationCount=50  LaunchCount=10  WarmupCount=10

|             Method |      Mean |     Error |     StdDev |    Median |   Gen 0 |   Gen 1 | Gen 2 | Allocated |
|------------------- |----------:|----------:|-----------:|----------:|--------:|--------:|------:|----------:|
|       EncodeIntNew |  96.29 us |  1.084 us |   7.144 us |  94.84 us | 17.0898 |  0.7324 |     - |  70.16 KB |
|       EncodeIntOld | 100.41 us |  1.401 us |   9.045 us |  98.48 us | 20.9961 |  1.3428 |     - |  85.96 KB |
|       DecodeIntNew |  98.57 us |  1.100 us |   7.138 us |  97.03 us | 29.7852 |  0.1221 |     - | 121.66 KB |
|       DecodeIntOld |  95.92 us |  2.052 us |  13.479 us |  92.22 us | 30.2734 |  0.7324 |     - |    124 KB |
|  EncodeIntBatchNew | 380.08 us |  4.803 us |  31.413 us | 372.23 us | 48.8281 | 16.1133 |     - | 222.31 KB |
|  EncodeIntBatchOld | 396.33 us | 22.300 us | 145.207 us | 345.26 us | 42.9688 | 10.7422 |     - |  175.9 KB |
|  DecodeIntBatchNew | 384.45 us |  7.222 us |  46.406 us | 374.18 us | 89.8438 | 30.7617 |     - | 417.18 KB |
|  DecodeIntBatchOld | 377.11 us |  7.578 us |  49.293 us | 364.51 us | 83.9844 | 29.7852 |     - |  391.4 KB |
|    EncodeDoubleNew | 107.05 us |  1.568 us |  10.254 us | 105.26 us | 17.0898 |  0.7324 |     - |  70.16 KB |
|    EncodeDoubleOld | 103.40 us |  1.763 us |  11.407 us | 100.66 us | 20.9961 |  1.2207 |     - |  86.64 KB |
|    DecodeDoubleNew | 126.36 us |  3.274 us |  21.529 us | 122.68 us | 29.7852 |  2.6855 |     - | 122.44 KB |
|    DecodeDoubleOld | 111.34 us |  3.084 us |  20.237 us | 105.89 us | 30.2734 |  0.7324 |     - |    124 KB |
|   EncodeSummaryNew | 197.33 us |  2.592 us |  17.226 us | 193.64 us | 34.6680 |  8.3008 |     - | 142.23 KB |
|   EncodeSummaryOld | 198.28 us |  2.485 us |  16.546 us | 195.11 us | 39.7949 |       - |     - | 163.89 KB |
|   DecodeSummaryNew | 463.65 us |  7.493 us |  49.265 us | 452.40 us | 67.8711 | 21.4844 |     - | 279.47 KB |
|   DecodeSummaryOld | 453.44 us |  7.655 us |  50.492 us | 443.31 us | 70.8008 |  7.3242 |     - | 290.41 KB |
| EncodeHistogramNew | 183.62 us |  2.214 us |  14.495 us | 180.74 us | 39.0625 | 10.2539 |     - |  164.7 KB |
| EncodeHistogramOld | 184.94 us |  3.447 us |  22.859 us | 180.61 us | 40.2832 |  0.4883 |     - | 165.45 KB |
| DecodeHistogramNew | 264.42 us |  3.420 us |  22.344 us | 258.24 us | 66.8945 | 21.4844 |     - | 296.87 KB |
| DecodeHistogramOld | 277.53 us |  2.317 us |  15.051 us | 274.77 us | 69.3359 | 18.5547 |     - | 299.21 KB |
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
