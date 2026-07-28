// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace OpenTelemetry.BlazorWasm.Tests;

/// <summary>
/// Manages the Playwright browser lifecycle for a single test and captures a
/// screenshot, trace and video when the test fails to aid debugging.
/// </summary>
internal sealed class BrowserFixture(ITestOutputHelper outputHelper)
{
    private readonly ITestOutputHelper outputHelper = outputHelper;

    private static bool IsRunningInGitHubActions
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    private static string ArtifactsDirectory
        => Path.Combine(AppContext.BaseDirectory, "playwright");

    public async Task WithPageAsync(Func<IPage, Task> action, [CallerMemberName] string? testName = null)
    {
        var name = testName ?? "test";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await CreateBrowserAsync(playwright);

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            RecordVideoDir = Path.Combine(ArtifactsDirectory, "videos"),
        });

        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
            Title = name,
        });

        var page = await context.NewPageAsync();

        page.Console += (_, e) => this.outputHelper.WriteLine(e.Text);
        page.PageError += (_, e) => this.outputHelper.WriteLine(e);

        var failed = false;

        try
        {
            await action(page);
        }
        catch (Exception)
        {
            failed = true;
            await this.TryCaptureScreenshotAsync(page, name);
            throw;
        }
        finally
        {
            var tracePath = failed ? Path.Combine(ArtifactsDirectory, "traces", GenerateFileName(name, ".zip")) : null;
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });

            if (tracePath is not null)
            {
                this.outputHelper.WriteLine($"Trace saved to {tracePath}.");
            }

            await this.TryCaptureVideoAsync(page, name, failed);
        }
    }

    private static async Task<IBrowser> CreateBrowserAsync(IPlaywright playwright)
    {
        var options = new BrowserTypeLaunchOptions { Headless = true };

        if (OperatingSystem.IsLinux() && IsRunningInGitHubActions)
        {
            // Workaround for Chromium crashes on Linux CI runners with a limited /dev/shm.
            options.Args = ["--disable-dev-shm-usage", "--no-sandbox"];
        }

        return await playwright.Chromium.LaunchAsync(options);
    }

    private static string GenerateFileName(string testName, string extension)
    {
        var os =
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "macos" :
            OperatingSystem.IsWindows() ? "windows" :
            "other";

        var utcNow = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
        return $"{testName}_chromium_{os}_{utcNow}{extension}";
    }

    private async Task TryCaptureScreenshotAsync(IPage page, string testName)
    {
        try
        {
            var path = Path.Combine(ArtifactsDirectory, "screenshots", GenerateFileName(testName, ".png"));
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path });
            this.outputHelper.WriteLine($"Screenshot saved to {path}.");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            this.outputHelper.WriteLine("Failed to capture screenshot: " + ex);
        }
    }

    private async Task TryCaptureVideoAsync(IPage page, string testName, bool failed)
    {
        if (page.Video is null)
        {
            return;
        }

        try
        {
            await page.CloseAsync();

            if (!failed)
            {
                await page.Video.DeleteAsync();
                return;
            }

            var path = Path.Combine(ArtifactsDirectory, "videos", GenerateFileName(testName, ".webm"));
            await page.Video.SaveAsAsync(path);
            this.outputHelper.WriteLine($"Video saved to {path}.");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            this.outputHelper.WriteLine("Failed to capture video: " + ex);
        }
    }
}
