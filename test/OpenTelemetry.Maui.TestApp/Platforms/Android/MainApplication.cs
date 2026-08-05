// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Android.App;
using Android.Runtime;

namespace OpenTelemetry.Maui.TestApp;

/// <summary>
/// The Android head of the MAUI app. .NET for Android creates this as the
/// process starts and <see cref="MauiApplication"/> builds the MAUI application
/// host from <see cref="MauiProgram"/>, which the on-device tests run against.
/// </summary>
/// <remarks>
/// The app declares no activity: the tests are driven by
/// <see cref="TestInstrumentation"/> rather than by any user interface. MAUI
/// builds the host from <c>AttachBaseContext</c>, which .NET for Android calls
/// before the instrumentation starts, so the host is always ready by the time
/// the tests run.
/// </remarks>
[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
