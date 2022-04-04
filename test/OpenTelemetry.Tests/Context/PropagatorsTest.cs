// <copyright file="PropagatorsTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests
{
    public class PropagatorsTest : IDisposable
    {
        public PropagatorsTest()
        {
            Propagators.Reset();
        }

        [Fact]
        public void DefaultTextMapPropagatorIsNoop()
        {
            Assert.IsType<NoopTextMapPropagator>(Propagators.DefaultTextMapPropagator);
            Assert.Same(Propagators.DefaultTextMapPropagator, Propagators.DefaultTextMapPropagator);
        }

        [Fact]
        public void CanSetPropagator()
        {
            var testPropagator = new TestPropagator(string.Empty, string.Empty);
            Propagators.DefaultTextMapPropagator = testPropagator;
            Assert.Same(testPropagator, Propagators.DefaultTextMapPropagator);
        }

        public void Dispose()
        {
            Propagators.Reset();
            GC.SuppressFinalize(this);
        }
    }
}
