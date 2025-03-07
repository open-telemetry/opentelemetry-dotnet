// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTracing.Propagation;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

internal sealed class TestFormatTextMap : IFormat<ITextMap>
{
}
