// <copyright file="TestListenerHandler.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Tests
{
    internal class TestListenerHandler : ListenerHandler
    {
        public int OnStartInvokedCount = 0;
        public int OnStopInvokedCount = 0;
        public int OnExceptionInvokedCount = 0;
        public int OnCustomInvokedCount = 0;

        public TestListenerHandler(string sourceName)
            : base(sourceName)
        {
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            this.OnStartInvokedCount++;
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            this.OnStopInvokedCount++;
        }

        public override void OnException(Activity activity, object payload)
        {
            this.OnExceptionInvokedCount++;
        }

        public override void OnCustom(string name, Activity activity, object payload)
        {
            this.OnCustomInvokedCount++;
        }
    }
}
