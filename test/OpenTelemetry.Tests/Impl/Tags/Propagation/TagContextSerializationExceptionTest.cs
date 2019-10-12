﻿// <copyright file="TagContextSerializationExceptionTest.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Tags.Propagation.Test
{
    public class TagContextSerializationExceptionTest
    {
        [Fact]
        public void CreateWithMessage()
        {
            Assert.Equal("my message", new TagContextSerializationException("my message").Message);
        }

        [Fact]
        public void CreateWithMessageAndCause()
        {
            var cause = new Exception();
            var exception = new TagContextSerializationException("my message", cause);
            Assert.Equal("my message", exception.Message);
            Assert.Equal(cause, exception.InnerException);
        }
    }
}
