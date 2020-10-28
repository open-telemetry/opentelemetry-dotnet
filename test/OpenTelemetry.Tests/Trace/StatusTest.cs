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

namespace OpenTelemetry.Trace.Tests
{
    public class StatusTest
    {
        [Fact]
        public void Status_Ok()
        {
            Assert.Equal(StatusCode.Ok, Status.Ok.StatusCode);
            Assert.Null(Status.Ok.Description);
        }

        [Fact]
        public void CreateStatus_WithDescription()
        {
            var status = Status.Error.WithDescription("This is an error.");
            Assert.Equal(StatusCode.Error, status.StatusCode);
            Assert.Equal("This is an error.", status.Description);
        }

        [Fact]
        public void Equality()
        {
            var status1 = new Status(StatusCode.Ok);
            var status2 = new Status(StatusCode.Ok);
            object status3 = new Status(StatusCode.Ok);

            Assert.Equal(status1, status2);
            Assert.True(status1 == status2);
            Assert.True(status1.Equals(status3));
        }

        [Fact]
        public void Equality_WithDescription()
        {
            var status1 = new Status(StatusCode.Error, "error");
            var status2 = new Status(StatusCode.Error, "error");

            Assert.Equal(status1, status2);
            Assert.True(status1 == status2);
        }

        [Fact]
        public void Not_Equality()
        {
            var status1 = new Status(StatusCode.Ok);
            var status2 = new Status(StatusCode.Error);
            object notStatus = 1;

            Assert.NotEqual(status1, status2);
            Assert.True(status1 != status2);
            Assert.False(status1.Equals(notStatus));
        }

        [Fact]
        public void Not_Equality_WithDescription1()
        {
            var status1 = new Status(StatusCode.Ok, "ok");
            var status2 = new Status(StatusCode.Error, "error");

            Assert.NotEqual(status1, status2);
            Assert.True(status1 != status2);
        }

        [Fact]
        public void Not_Equality_WithDescription2()
        {
            var status1 = new Status(StatusCode.Ok);
            var status2 = new Status(StatusCode.Error, "error");

            Assert.NotEqual(status1, status2);
            Assert.True(status1 != status2);
        }

        [Fact]
        public void TestToString()
        {
            var status = new Status(StatusCode.Ok);
            Assert.Equal($"Status{{StatusCode={status.StatusCode}, Description={status.Description}}}", status.ToString());
        }

        [Fact]
        public void TestGetHashCode()
        {
            var status = new Status(StatusCode.Ok);
            Assert.NotEqual(0, status.GetHashCode());
        }
    }
}
