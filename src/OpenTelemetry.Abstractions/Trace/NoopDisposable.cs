// <copyright file="NoopDisposable.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System;

    /// <summary>
    /// An implmentation of IDisposable that does nothing.
    /// </summary>
    public class NoopDisposable : IDisposable
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="NoopDisposable"/> class from being created.
        /// </summary>
        private NoopDisposable()
        {
        }

        public static IDisposable Instance { get; } = new NoopDisposable();

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
