// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace SensitiveLogging;

public interface IPiiLogger : ILogger
{
}

public interface IPiiLogger<out TCategoryName> : IPiiLogger
{
}
