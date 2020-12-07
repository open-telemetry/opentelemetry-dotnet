// <copyright file="TraceVersionTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Trace
{
    public class TraceVersionTest
    {
        [Theory]
        [InlineData("1")]
        [InlineData("1.1")]
        [InlineData("1.1.1")]
        [InlineData("1.1.1.1")]
        [InlineData("1234.1234.1234.1234")]
        [InlineData("*")]
        [InlineData("1.*")]
        [InlineData("1.1.*")]
        [InlineData("1.1.1.*")]
        public void ValidateValidVersions(string version)
        {
            var traceSource = new TraceVersion("test", version);
            Assert.Equal(version, traceSource.MinVersion);
        }

        [Theory]
        [InlineData("*.1")]
        [InlineData("*.*.1")]
        [InlineData("*.*.*.1")]
        [InlineData("*.*")]
        [InlineData("1.*.1")]
        [InlineData("a")]
        [InlineData("a.b")]
        [InlineData("a.b.c")]
        [InlineData("a.b.c.d")]
        public void ValidateInvalidVersions(string version)
        {
            Assert.Throws<ArgumentException>(() => { var traceSource = new TraceVersion("test", version); });
        }

        [Theory]
        [InlineData("1.0", "", "", true)]
        [InlineData("1.0", null, null, true)]
        [InlineData("1.0", "1.0", null, true)]
        [InlineData("1.0", "0.9", null, false)]
        [InlineData("1.0", null, "1.0", true)]
        [InlineData("1.0", null, "0.9", false)]
        [InlineData("1.0", "1.0", "1.0", true)]
        [InlineData("1.0", "1.0", "1.1", true)]
        [InlineData("1.0.0", "1.0", "1.0", true)]
        [InlineData("1.0.0", "1.0", "1.1", true)]
        [InlineData("1.0.0", "*", null, true)]
        [InlineData("1.0.0", null, "*", true)]
        public void CompareVersions(string current, string minVersion, string maxVersion, bool expected)
        {
            Assert.Equal(expected, VersionHelper.Compare(current, minVersion, maxVersion));
        }
    }
}
