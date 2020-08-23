﻿// <copyright file="TestActivityProcessor.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests
{
    internal class TestActivityProcessor : ActivityProcessor
    {
        public Action<Activity> StartAction;
        public Action<Activity> EndAction;

        public TestActivityProcessor()
        {
        }

        public TestActivityProcessor(Action<Activity> onStart, Action<Activity> onEnd)
        {
            this.StartAction = onStart;
            this.EndAction = onEnd;
        }

        public bool ShutdownCalled { get; private set; } = false;

        public bool ForceFlushCalled { get; private set; } = false;

        public bool DisposedCalled { get; private set; } = false;

        public override void OnStart(Activity span)
        {
            this.StartAction?.Invoke(span);
        }

        public override void OnEnd(Activity span)
        {
            this.EndAction?.Invoke(span);
        }

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            this.ForceFlushCalled = true;
            return true;
        }

        protected override void OnShutdown(int timeoutMilliseconds)
        {
            this.ShutdownCalled = true;
        }

        protected override void Dispose(bool disposing)
        {
            this.DisposedCalled = true;
        }
    }
}
