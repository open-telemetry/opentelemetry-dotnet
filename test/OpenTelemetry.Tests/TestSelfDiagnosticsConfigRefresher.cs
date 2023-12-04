// <copyright file="TestSelfDiagnosticsConfigRefresher.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests;

internal class TestSelfDiagnosticsConfigRefresher(Stream stream = null) : SelfDiagnosticsConfigRefresher
{
    private readonly Stream stream = stream;

    public bool TryGetLogStreamCalled { get; private set; }

    public override bool TryGetLogStream(int byteCount, [NotNullWhen(true)] out Stream stream, out int availableByteCount)
    {
        this.TryGetLogStreamCalled = true;
        stream = this.stream;
        availableByteCount = 0;
        return true;
    }
}
