// <copyright file="OpenTelemetryLoggerOptionsSdkTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public class OpenTelemetryLoggerOptionsSdkTests
{
    [Fact]
    public void CreateLoggerProviderBuilderBuildValidProviderTest()
    {
        List<LogRecord> exportedItems = new();

        using var provider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        Assert.NotNull(provider);

        provider.CreateEmitter().Emit(new()
        {
            Message = "Hello world",
        });

        Assert.Single(exportedItems);
    }

    [Fact]
    public void CreateLoggerProviderBuilderExtensionPointsTest()
    {
        int optionsConfigureInvocations = 0;
        OpenTelemetryLoggerProvider? providerFromConfigureCallback = null;

        var returnedOptions = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(new CustomProcessor())
            .AddProcessor<CustomProcessor>()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TestClass1>();
                services.Configure<OpenTelemetryLoggerOptions>(o =>
                {
                    optionsConfigureInvocations++;

                    Assert.Null(o.Services);

                    Assert.Throws<NotSupportedException>(() => o.ConfigureServices(s => { }));

                    o.ConfigureResource(r => r.AddAttributes(new Dictionary<string, object> { ["key1"] = "value1" }));

                    o.ConfigureProvider((sp, p) => optionsConfigureInvocations++);
                });
            })
            .ConfigureProvider((sp, p) =>
            {
                Assert.NotNull(sp);

                providerFromConfigureCallback = p;

                Assert.NotNull(sp.GetService<TestClass1>());
            });

        using var provider = returnedOptions.Build();

        Assert.NotNull(provider);

        Assert.Throws<NotSupportedException>(() => returnedOptions.ConfigureServices(s => { }));
        Assert.Throws<NotSupportedException>(() => returnedOptions.ConfigureResource(r => { }));
        Assert.Throws<NotSupportedException>(() => returnedOptions.ConfigureProvider((sp, p) => { }));
        Assert.Throws<NotSupportedException>(() => returnedOptions.Build());

        Assert.Equal(2, optionsConfigureInvocations);
        Assert.NotNull(providerFromConfigureCallback);
        Assert.Equal(provider, providerFromConfigureCallback);

        Assert.NotNull(provider.Resource?.Attributes);
        Assert.Contains(provider.Resource!.Attributes, kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");

        var processor = provider.Processor as CompositeProcessor<LogRecord>;
        Assert.NotNull(processor);

        int count = 0;
        var current = processor?.Head;
        while (current != null)
        {
            count++;
            current = current.Next;
        }

        Assert.Equal(2, count);
    }

    private sealed class TestClass1
    {
    }

    private sealed class CustomProcessor : BaseProcessor<LogRecord>
    {
    }
}
