// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Maui.TestApp;

/// <summary>
/// The MAUI application. Nothing launches an activity during the on-device test
/// run so no window is ever created, but MAUI resolves the application from the
/// container as it starts up, which is what the tests assert happened.
/// </summary>
public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
        => new(new ContentPage() { Title = "OpenTelemetry.Maui.TestApp" });
}
