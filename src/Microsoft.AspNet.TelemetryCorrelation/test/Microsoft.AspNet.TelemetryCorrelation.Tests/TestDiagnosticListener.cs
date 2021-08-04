// <copyright file="TestDiagnosticListener.cs" company="Microsoft">
// Copyright (c) .NET Foundation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// </copyright>

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    using System;
    using System.Collections.Generic;

    internal class TestDiagnosticListener : IObserver<KeyValuePair<string, object>>
    {
        private readonly Action<KeyValuePair<string, object>> onNextCallBack;

        public TestDiagnosticListener(Action<KeyValuePair<string, object>> onNext)
        {
            this.onNextCallBack = onNext;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            this.onNextCallBack?.Invoke(value);
        }
    }
}