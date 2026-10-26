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
/// builds the host from <c>OnCreate</c>, which Android calls concurrently with
/// the instrumentation starting, so <see cref="TestInstrumentation"/> waits for
/// it to complete before running the tests.
/// </remarks>
[Application]
public class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
