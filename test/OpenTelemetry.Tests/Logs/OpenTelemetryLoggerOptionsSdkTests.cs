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

        provider.GetLogger().EmitLog(new()
        {
            Body = "Hello world",
        });

        Assert.Single(exportedItems);
    }

    [Fact]
    public void CreateLoggerProviderBuilderExtensionPointsTest()
    {
        int configureBuilderInvocations = 0;

        var returnedBuilder = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(new CustomProcessor())
            .AddProcessor<CustomProcessor>()
            .ConfigureServices(services =>
            {
                services.AddSingleton<TestClass1>();
            })
            .ConfigureBuilder((sp, o) =>
            {
                configureBuilderInvocations++;

                Assert.Throws<NotSupportedException>(() => o.ConfigureServices(s => { }));

                o.ConfigureResource(r => r.AddAttributes(new Dictionary<string, object> { ["key1"] = "value1" }));

                o.ConfigureBuilder((sp, b) => configureBuilderInvocations++);
            })
            .ConfigureBuilder((sp, p) =>
            {
                Assert.NotNull(sp);

                Assert.NotNull(sp.GetService<TestClass1>());
            });

        using var provider = returnedBuilder.Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        Assert.Throws<NotSupportedException>(() => returnedBuilder.ConfigureServices(s => { }));
        Assert.Throws<NotSupportedException>(() => returnedBuilder.ConfigureResource(r => { }));
        Assert.Throws<NotSupportedException>(() => returnedBuilder.ConfigureBuilder((sp, p) => { }));
        Assert.Throws<NotSupportedException>(() => returnedBuilder.Build());

        Assert.Equal(2, configureBuilderInvocations);

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
