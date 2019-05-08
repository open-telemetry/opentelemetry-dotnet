// <copyright file="SpanContextParseExceptionTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Propagation.Test
{
    using System;
    using Xunit;

    public class SpanContextParseExceptionTest
    {
        [Fact]
        public void CreateWithMessage()
        {
            Assert.Equal("my message", new SpanContextParseException("my message").Message);
        }

        [Fact]
        public void createWithMessageAndCause()
        {
            Exception cause = new Exception();
            SpanContextParseException parseException = new SpanContextParseException("my message", cause);
            Assert.Equal("my message", parseException.Message);
            Assert.Equal(cause, parseException.InnerException);
        }
    }
}
