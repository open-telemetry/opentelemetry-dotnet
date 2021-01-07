// <copyright file="GrpcTagHelperTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
    public class GrpcTagHelperTests
    {
        [Fact]
        public void GrpcTagHelper_GetGrpcMethodFromActivity()
        {
            var grpcMethod = "/some.service/somemethod";
            var activity = new Activity("operationName");
            activity.SetTag(GrpcTagHelper.GrpcMethodTagName, grpcMethod);

            var result = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

            Assert.Equal(grpcMethod, result);
        }

        [Theory]
        [InlineData("Package.Service/Method", true, "Package.Service", "Method")]
        [InlineData("/Package.Service/Method", true, "Package.Service", "Method")]
        [InlineData("/ServiceWithNoPackage/Method", true, "ServiceWithNoPackage", "Method")]
        [InlineData("/Some.Package.Service/Method", true, "Some.Package.Service", "Method")]
        [InlineData("Invalid", false, "", "")]
        public void GrpcTagHelper_TryParseRpcServiceAndRpcMethod(string grpcMethod, bool isSuccess, string expectedRpcService, string expectedRpcMethod)
        {
            var success = GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod);

            Assert.Equal(isSuccess, success);
            Assert.Equal(expectedRpcService, rpcService);
            Assert.Equal(expectedRpcMethod, rpcMethod);
        }

        [Fact]
        public void GrpcTagHelper_GetGrpcStatusCodeFromActivity()
        {
            var activity = new Activity("operationName");
            activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, "0");

            bool validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out int status);
            Assert.True(validConversion);

            var statusCode = GrpcTagHelper.ResolveSpanStatusForGrpcStatusCode(status);
            activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, status);

            Assert.Equal(StatusCode.Unset, statusCode.StatusCode);
            Assert.Equal(status, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }

        [Fact]
        public void GrpcTagHelper_GetGrpcStatusCodeFromEmptyActivity()
        {
            var activity = new Activity("operationName");

            bool validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out int status);
            Assert.False(validConversion);
            Assert.Equal(-1, status);
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
        }
    }
}
