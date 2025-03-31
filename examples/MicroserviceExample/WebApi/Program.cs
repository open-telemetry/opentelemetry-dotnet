// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace WebApi;

internal static class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://*:5000").UseStartup<Startup>();
            });
}
