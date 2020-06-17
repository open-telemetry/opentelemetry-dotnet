// <copyright file="StatusTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    public class StatusTest
    {
        [Fact]
        public void Status_Ok()
        {
            Assert.Equal(StatusCanonicalCode.Ok, Status.Ok.CanonicalCode);
            Assert.Null(Status.Ok.Description);
            Assert.True(Status.Ok.IsOk);
        }

        [Fact]
        public void CreateStatus_WithDescription()
        {
            var status = Status.Unknown.WithDescription("This is an error.");
            Assert.Equal(StatusCanonicalCode.Unknown, status.CanonicalCode);
            Assert.Equal("This is an error.", status.Description);
            Assert.False(status.IsOk);
        }

        [Fact]
        public void Equality()
        {
            var status1 = new Status(StatusCanonicalCode.Ok);
            var status2 = new Status(StatusCanonicalCode.Ok);

            Assert.Equal(status1, status2);
            Assert.True(status1 == status2);
        }

        [Fact]
        public void Equality_WithDescription()
        {
            var status1 = new Status(StatusCanonicalCode.Unknown, "error");
            var status2 = new Status(StatusCanonicalCode.Unknown, "error");

            Assert.Equal(status1, status2);
            Assert.True(status1 == status2);
        }

        [Fact]
        public void Not_Equality()
        {
            var status1 = new Status(StatusCanonicalCode.Ok);
            var status2 = new Status(StatusCanonicalCode.Unknown);

            Assert.NotEqual(status1, status2);
            Assert.True(status1 != status2);
        }

        [Fact]
        public void Not_Equality_WithDescription1()
        {
            var status1 = new Status(StatusCanonicalCode.Ok, "ok");
            var status2 = new Status(StatusCanonicalCode.Unknown, "error");

            Assert.NotEqual(status1, status2);
            Assert.True(status1 != status2);
        }

        [Fact]
        public void Not_Equality_WithDescription2()
        {
            var status1 = new Status(StatusCanonicalCode.Ok);
            var status2 = new Status(StatusCanonicalCode.Unknown, "error");

            Assert.NotEqual(status1, status2);
            Assert.True(status1 != status2);
        }
    }
}
