// <copyright file="RuntimeContextTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Tests
{
    public class RuntimeContextTest : IDisposable
    {
        public RuntimeContextTest()
        {
            RuntimeContext.Clear();
        }

        [Fact]
        public static void RegisterSlotWithSameName()
        {
            var slot = RuntimeContext.RegisterSlot<bool>("testslot");
            Assert.NotNull(slot);
            Assert.Throws<InvalidOperationException>(() => RuntimeContext.RegisterSlot<bool>("testslot"));
        }

        [Fact]
        public void GetSlotReturnsNullForNonExistingSlot()
        {
            var slot = RuntimeContext.GetSlot<bool>("testslot");
            Assert.Null(slot);
        }

        [Fact]
        public void GetSlotReturnsNullWhenTypeNotMatchingExistingSlot()
        {
            RuntimeContext.RegisterSlot<bool>("testslot");
            var slot = RuntimeContext.GetSlot<int>("testslot");
            Assert.Null(slot);
        }

        [Fact]
        public void RegisterAndGetSlot()
        {
            var expectedSlot = RuntimeContext.RegisterSlot<int>("testslot");
            Assert.NotNull(expectedSlot);
            expectedSlot.Set(100);
            var actualSlot = RuntimeContext.GetSlot<int>("testslot");
            Assert.Same(expectedSlot, actualSlot);
            Assert.Equal(100, expectedSlot.Get());
        }

        public void Dispose()
        {
            RuntimeContext.Clear();
        }
    }
}
