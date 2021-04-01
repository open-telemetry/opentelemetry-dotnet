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

using BenchmarkDotNet.Attributes;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.201
  [Host]     : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT
  Job-KYFGEQ : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT

IterationCount=1  LaunchCount=1  WarmupCount=1

|             Method |     Mean | Error |   Gen 0 |   Gen 1 | Gen 2 | Allocated |
|------------------- |---------:|------:|--------:|--------:|------:|----------:|
|       EncodeIntNew | 137.3 us |    NA | 17.0898 |  1.4648 |     - |  70.16 KB |
|       EncodeIntOld | 121.4 us |    NA | 20.9961 |  1.9531 |     - |  85.96 KB |
|       DecodeIntNew | 131.7 us |    NA | 29.7852 |  0.1221 |     - | 121.66 KB |
|       DecodeIntOld | 141.6 us |    NA | 30.2734 |  0.7324 |     - |    124 KB |
|  EncodeIntBatchNew | 447.2 us |    NA | 48.8281 | 16.1133 |     - | 222.31 KB |
|  EncodeIntBatchOld | 374.5 us |    NA | 42.9688 | 10.7422 |     - |  175.9 KB |
|  DecodeIntBatchNew | 427.5 us |    NA | 89.3555 | 32.7148 |     - | 417.18 KB |
|  DecodeIntBatchOld | 394.5 us |    NA | 83.0078 | 28.3203 |     - |  391.4 KB |
|    EncodeDoubleNew | 151.5 us |    NA | 17.0898 |  1.4648 |     - |  70.16 KB |
|    EncodeDoubleOld | 123.8 us |    NA | 20.9961 |  1.2207 |     - |  86.64 KB |
|    DecodeDoubleNew | 132.1 us |    NA | 29.7852 |  2.6855 |     - | 122.44 KB |
|    DecodeDoubleOld | 159.5 us |    NA | 30.2734 |  0.7324 |     - |    124 KB |
|   EncodeSummaryNew | 245.5 us |    NA | 34.6680 |  8.5449 |     - | 142.23 KB |
|   EncodeSummaryOld | 239.5 us |    NA | 39.5508 |       - |     - | 163.89 KB |
|   DecodeSummaryNew | 480.7 us |    NA | 67.3828 | 21.4844 |     - | 279.47 KB |
|   DecodeSummaryOld | 515.3 us |    NA | 70.3125 | 14.6484 |     - | 290.41 KB |
| EncodeHistogramNew | 209.6 us |    NA | 39.0625 | 10.2539 |     - |  164.7 KB |
| EncodeHistogramOld | 237.2 us |    NA | 40.2832 |  0.4883 |     - | 165.45 KB |
| DecodeHistogramNew | 313.0 us |    NA | 66.8945 | 21.4844 |     - | 296.87 KB |
| DecodeHistogramOld | 398.6 us |    NA | 70.3125 | 18.5547 |     - | 299.21 KB |
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
        private byte[] oldIntBytes;
        private byte[] newIntBytes;
        private byte[] oldDoubleBytes;
        private byte[] newDoubleBytes;
        private byte[] oldIntBatchBytes;
        private byte[] newIntBatchBytes;
        private byte[] oldSummaryBytes;
        private byte[] newSummaryBytes;
        private byte[] oldHistogramBytes;
        private byte[] newHistogramBytes;

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

            this.newIntBytes = ProtoNew.Encode(false, this.resources, this.labels, 1, 100, 1);
            this.oldIntBytes = ProtoOld.Encode(false, this.resources, this.labels, 1, 100, 1);

            this.newDoubleBytes = ProtoNew.Encode(true, this.resources, this.labels, 1, 100, 1);
            this.oldDoubleBytes = ProtoOld.Encode(true, this.resources, this.labels, 1, 100, 1);

            this.newIntBatchBytes = ProtoNew.Encode(false, this.resources, this.labels, 1, 100, 5);
            this.oldIntBatchBytes = ProtoOld.Encode(false, this.resources, this.labels, 1, 100, 5);

            this.newSummaryBytes = ProtoNew.EncodeSummary(this.resources, this.labels, 1, 100, 1, 10);
            this.oldSummaryBytes = ProtoOld.EncodeSummary(this.resources, this.labels, 1, 100, 1, 10);

            this.newHistogramBytes = ProtoNew.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 2, 2);
            this.oldHistogramBytes = ProtoOld.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 2, 2);
        }

        [Benchmark]
        public int EncodeIntNew()
        {
            var bytes = ProtoNew.Encode(false, this.resources, this.labels, 1, 100, 1);
            return bytes.Length;
        }

        [Benchmark]
        public int EncodeIntOld()
        {
            var bytes = ProtoOld.Encode(false, this.resources, this.labels, 1, 100, 1);
            return bytes.Length;
        }

        [Benchmark]
        public int DecodeIntNew()
        {
            var extracts = ProtoNew.Decode(this.newIntBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int DecodeIntOld()
        {
            var extracts = ProtoOld.Decode(this.oldIntBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int EncodeIntBatchNew()
        {
            var bytes = ProtoNew.Encode(false, this.resources, this.labels, 1, 100, 5);
            return bytes.Length;
        }

        [Benchmark]
        public int EncodeIntBatchOld()
        {
            var bytes = ProtoOld.Encode(false, this.resources, this.labels, 1, 100, 5);
            return bytes.Length;
        }

        [Benchmark]
        public int DecodeIntBatchNew()
        {
            var extracts = ProtoNew.Decode(this.newIntBatchBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int DecodeIntBatchOld()
        {
            var extracts = ProtoOld.Decode(this.oldIntBatchBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int EncodeDoubleNew()
        {
            var bytes = ProtoNew.Encode(true, this.resources, this.labels, 1, 100, 1);
            return bytes.Length;
        }

        [Benchmark]
        public int EncodeDoubleOld()
        {
            var bytes = ProtoOld.Encode(true, this.resources, this.labels, 1, 100, 1);
            return bytes.Length;
        }

        [Benchmark]
        public int DecodeDoubleNew()
        {
            var extracts = ProtoNew.Decode(this.newDoubleBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int DecodeDoubleOld()
        {
            var extracts = ProtoOld.Decode(this.oldDoubleBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int EncodeSummaryNew()
        {
            var bytes = ProtoNew.EncodeSummary(this.resources, this.labels, 1, 100, 1, 10);
            return bytes.Length;
        }

        [Benchmark]
        public int EncodeSummaryOld()
        {
            var bytes = ProtoOld.EncodeSummary(this.resources, this.labels, 1, 100, 1, 10);
            return bytes.Length;
        }

        [Benchmark]
        public int DecodeSummaryNew()
        {
            var extracts = ProtoNew.Decode(this.newSummaryBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int DecodeSummaryOld()
        {
            var extracts = ProtoOld.Decode(this.oldSummaryBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int EncodeHistogramNew()
        {
            var bytes = ProtoNew.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 2, 2);
            return bytes.Length;
        }

        [Benchmark]
        public int EncodeHistogramOld()
        {
            var bytes = ProtoOld.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 2, 2);
            return bytes.Length;
        }

        [Benchmark]
        public int DecodeHistogramNew()
        {
            var extracts = ProtoNew.Decode(this.newHistogramBytes);
            return extracts.Count;
        }

        [Benchmark]
        public int DecodeHistogramOld()
        {
            var extracts = ProtoOld.Decode(this.oldHistogramBytes);
            return extracts.Count;
        }
    }
}
