// <copyright file="CurrentSpanTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Tests
{
    public class CurrentSpanTests : IDisposable
    {
        private readonly Tracer tracer;

        public CurrentSpanTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            this.tracer = TracerProvider.Default.GetTracer(null);
        }

        [Fact]
        public void CurrentSpan_WhenNoContext()
        {
            Assert.False(Tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void CurrentSpan_WhenActivityExists()
        {
            _ = new Activity("foo").Start();
            Assert.True(Tracer.CurrentSpan.Context.IsValid);
        }

        public void Dispose()
        {
            Activity.Current = null;
            GC.SuppressFinalize(this);
        }
    }
}
