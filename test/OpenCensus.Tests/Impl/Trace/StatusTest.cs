// <copyright file="StatusTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Test
{
    using Xunit;

    public class StatusTest
    {
        [Fact]
        public void Status_Ok()
        {
            Assert.Equal(CanonicalCode.Ok, Status.Ok.CanonicalCode);
            Assert.Null(Status.Ok.Description);
            Assert.True(Status.Ok.IsOk);
        }

        [Fact]
        public void CreateStatus_WithDescription()
        {
            Status status = Status.Unknown.WithDescription("This is an error.");
            Assert.Equal(CanonicalCode.Unknown, status.CanonicalCode);
            Assert.Equal("This is an error.", status.Description);
            Assert.False(status.IsOk);
        }

        [Fact]
        public void Status_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // tester.addEqualityGroup(Status.OK, Status.OK.withDescription(null));
            // tester.addEqualityGroup(
            //    Status.CANCELLED.withDescription("ThisIsAnError"),
            //    Status.CANCELLED.withDescription("ThisIsAnError"));
            // tester.addEqualityGroup(Status.UNKNOWN.withDescription("This is an error."));
            // tester.testEquals();
        }
    }
}
