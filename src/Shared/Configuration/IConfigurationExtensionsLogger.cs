// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Microsoft.Extensions.Configuration;

internal interface IConfigurationExtensionsLogger
{
    void LogInvalidConfigurationValue(string key, string value);
}
