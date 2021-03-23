// <copyright file="StringBuilderPoolTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Tests.Internal
{
    public class StringBuilderPoolTest
    {
        [Fact]
        public void GetAndReturnSameInstance()
        {
            // Arrange
            var pool = new StringBuilderPool();

            var obj1 = pool.Get();
            pool.Return(obj1);

            // Act
            var obj2 = pool.Get();

            // Assert
            Assert.Same(obj1, obj2);
        }
    }
}
