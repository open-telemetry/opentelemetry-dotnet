// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// Implemented by an instrumentation instance which needs to observe the
/// provider it was registered with once that provider's <see
/// cref="Resources.Resource"/> has been resolved.
/// </summary>
/// <remarks>
/// Providers invoke <see cref="OnProviderBuilt"/> from their constructor,
/// after the resource has been built and while the registered
/// instrumentation is collected. Implementations must not throw and must not
/// block. The instance is disposed (when it implements <see
/// cref="IDisposable"/>) when the provider is shut down or disposed, as with
/// any other instrumentation.
/// </remarks>
internal interface IProviderBuiltCallback
{
    /// <summary>
    /// Called once the provider's resource has been resolved.
    /// </summary>
    /// <param name="provider">The provider being built.</param>
    void OnProviderBuilt(BaseProvider provider);
}
