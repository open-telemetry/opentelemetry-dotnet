// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace DedicatedLogging;

internal interface IDedicatedLogger : ILogger
{
}

internal interface IDedicatedLogger<out TCategoryName> : IDedicatedLogger
{
}
