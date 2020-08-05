// <copyright file="ThreadLocalRuntimeContextSlotTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Text;
using OpenTelemetry.Context;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Context
{
    public class ThreadLocalRuntimeContextSlotTests
    {
        [Fact]
        public void TestDispose()
        {
            using (var threadLocal = new ThreadLocalRuntimeContextSlot<int>("test"))
            {
                threadLocal.Set(1);
                Assert.Equal(1, threadLocal.Get());

                threadLocal.Set(2);
                Assert.Equal(2, threadLocal.Get());
            }
        }
    }
}
