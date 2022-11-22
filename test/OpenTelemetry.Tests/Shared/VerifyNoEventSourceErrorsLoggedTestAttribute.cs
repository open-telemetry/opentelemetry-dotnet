// <copyright file="VerifyNoEventSourceErrorsLoggedTestAttribute.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests.Shared
{
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Reflection;
    using Xunit;
    using Xunit.Sdk;

    /// <summary>
    /// Can be used to assert that no Errors were logged to EventSource during a unit test.
    /// </summary>
    internal class VerifyNoEventSourceErrorsLoggedTestAttribute : BeforeAfterTestAttribute
    {
        private readonly UnitTestEventListener myTestEventListener;

        public VerifyNoEventSourceErrorsLoggedTestAttribute(string eventSourceName)
        {
            this.myTestEventListener = new UnitTestEventListener(eventSourceName, EventLevel.Error);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            if (this.myTestEventListener.CapturedEvents.Any())
            {
                var eventNames = string.Join(",", this.myTestEventListener.CapturedEvents.Select(x => x.EventName));

                Assert.Fail($"EventSource Exception captured during test run: {eventNames}");
            }
        }
    }
}
