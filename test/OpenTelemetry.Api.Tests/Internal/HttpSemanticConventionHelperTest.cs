// <copyright file="HttpSemanticConventionHelperTest.cs" company="OpenTelemetry Authors">
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

using Xunit;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Api.Tests.Internal
{
    public class HttpSemanticConventionHelperTest
    {
        [Fact]
        public void VerifyFlags()
        {
            var testValue = HttpSemanticConvention.Dupe;
            Assert.True(testValue.HasFlag(HttpSemanticConvention.Old));
            Assert.True(testValue.HasFlag(HttpSemanticConvention.New));

            testValue = HttpSemanticConvention.Old;
            Assert.True(testValue.HasFlag(HttpSemanticConvention.Old));
            Assert.False(testValue.HasFlag(HttpSemanticConvention.New));

            testValue = HttpSemanticConvention.New;
            Assert.False(testValue.HasFlag(HttpSemanticConvention.Old));
            Assert.True(testValue.HasFlag(HttpSemanticConvention.New));
        }

        [Fact]
        public void VerifyEvaluate()
        {
            Assert.Equal(HttpSemanticConvention.Old, EvaluateValue(null));
            Assert.Equal(HttpSemanticConvention.Old, EvaluateValue(string.Empty));
            Assert.Equal(HttpSemanticConvention.Old, EvaluateValue("junk"));
            Assert.Equal(HttpSemanticConvention.Old, EvaluateValue("none"));
            Assert.Equal(HttpSemanticConvention.Old, EvaluateValue("NONE"));
            Assert.Equal(HttpSemanticConvention.New, EvaluateValue("http"));
            Assert.Equal(HttpSemanticConvention.New, EvaluateValue("HTTP"));
            Assert.Equal(HttpSemanticConvention.Dupe, EvaluateValue("http/dup"));
            Assert.Equal(HttpSemanticConvention.Dupe, EvaluateValue("HTTP/DUP"));
        }
    }
}
