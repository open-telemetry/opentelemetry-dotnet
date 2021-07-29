// <copyright file="Int128Test.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Implementation.Tests
{
    public class Int128Test
    {
        [Fact]
        public void Int128ConversionWorksAsExpected()
        {
            var id = ActivityTraceId.CreateFromBytes(new byte[] { 0x1a, 0x0f, 0x54, 0x63, 0x25, 0xa8, 0x56, 0x43, 0x1a, 0x4c, 0x24, 0xea, 0xa8, 0x60, 0xb0, 0xe8 });
            var int128 = new Int128(id);

            Assert.Equal<long>(unchecked(0x1a0f546325a85643), int128.High);
            Assert.Equal<long>(unchecked(0x1a4c24eaa860b0e8), int128.Low);
        }
    }
}
