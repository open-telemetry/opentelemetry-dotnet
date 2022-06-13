// <copyright file="CallerArgumentExpressionAttributeTests.cs" company="OpenTelemetry Authors">
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

using System.Runtime.CompilerServices;

using Xunit;

namespace OpenTelemetry.Tests.Internal
{
#if !NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Borrowed from: <see href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Runtime/tests/System/Runtime/CompilerServices/CallerArgumentExpressionAttributeTests.cs"/>.
    /// </summary>
    public class CallerArgumentExpressionAttributeTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("paramName")]
        public static void Ctor_ParameterName_Roundtrip(string value)
        {
            var caea = new CallerArgumentExpressionAttribute(value);
            Assert.Equal(value, caea.ParameterName);
        }

        [Fact]
        public static void BasicTest()
        {
            Assert.Equal("\"hello\"", GetValue("hello"));
            Assert.Equal("3 + 2", GetValue(3 + 2));
            Assert.Equal("new object()", GetValue(new object()));
        }

        private static string GetValue(object argument, [CallerArgumentExpression("argument")] string expr = null) => expr;
    }
#endif
}
