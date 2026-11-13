// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Internal;

internal sealed class SelfDiagnosticsOptionsChangeTokenSource(IConfiguration configuration)
    : ConfigurationChangeTokenSource<SelfDiagnosticsOptions>(
        Options.DefaultName,
        configuration.GetSection(SelfDiagnosticsOptions.ConfigurationSectionPath));
