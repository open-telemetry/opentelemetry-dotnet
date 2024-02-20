// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace DedicatedLogging;

public interface IDedicatedLogger : ILogger
{
}

public interface IDedicatedLogger<out TCategoryName> : IDedicatedLogger
{
}
