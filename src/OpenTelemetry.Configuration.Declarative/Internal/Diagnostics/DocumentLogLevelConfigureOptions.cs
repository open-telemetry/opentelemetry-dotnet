// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Applies the document's <c>log_level</c> to <see cref="SelfDiagnosticsOptions.MinimumLevel"/>.
/// </summary>
/// <param name="accessor">The declarative configuration document accessor.</param>
internal sealed class DocumentLogLevelConfigureOptions(DeclarativeConfigurationDocumentAccessor accessor)
    : IConfigureOptions<SelfDiagnosticsOptions>
{
    /// <inheritdoc/>
    public void Configure(SelfDiagnosticsOptions options)
    {
        try
        {
            if (accessor.GetDocument().Model.LogLevel.TryGetValue(out var severity))
            {
                options.MinimumLevel = severity.ToLogLevel();
            }
        }
        catch (DeclarativeConfigurationException)
        {
            // A document that failed to read applies nothing. The failure was reported when it was read.
        }
    }
}
