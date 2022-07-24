// <copyright file="ExceptionFilterTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

#if NETCOREAPP3_1
using TestApp.AspNetCore._3._1;
using TestApp.AspNetCore._3._1.Filters;
#endif
#if NET6_0
using TestApp.AspNetCore._6._0;
using TestApp.AspNetCore._6._0.Filters;
#endif
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    public sealed class ExceptionFilterTests
        : IClassFixture<WebApplicationFactory<Startup>>, IDisposable
    {
        private readonly WebApplicationFactory<Startup> factory;
        private TracerProvider tracerProvider = null;

        public ExceptionFilterTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task ShouldExportExceptionWithNoExceptionFilters()
        {
            var exportedItems = new List<Activity>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    (s) => ConfigureTestServices(s, 0, ref exportedItems)))
                .CreateClient())
            {
                // Act
                var response = await client.GetAsync("/api/error");

                WaitForActivityExport(exportedItems, 1);
            }

            // Assert
            AssertException(exportedItems);
        }

        [Fact]
        public async Task ShouldExportActivityWithOneExceptionFilter()
        {
            var exportedItems = new List<Activity>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    (s) => ConfigureTestServices(s, 1, ref exportedItems)))
                .CreateClient())
            {
                // Act
                var response = await client.GetAsync("/api/error");

                WaitForActivityExport(exportedItems, 1);
            }

            // Assert
            AssertException(exportedItems);
        }

        [Fact]
        public async Task ShouldExportSingleActivityEvenWithTwoExceptionFilters()
        {
            var exportedItems = new List<Activity>();

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    (s) => ConfigureTestServices(s, 2, ref exportedItems)))
                .CreateClient())
            {
                // Act
                var response = await client.GetAsync("/api/error");

                WaitForActivityExport(exportedItems, 1);
            }

            // Assert
            AssertException(exportedItems);
        }

        public void Dispose()
        {
            this.tracerProvider?.Dispose();
        }

        private static void WaitForActivityExport(List<Activity> exportedItems, int count)
        {
            // We need to let End callback execute as it is executed AFTER response was returned.
            // In unit tests environment there may be a lot of parallel unit tests executed, so
            // giving some breezing room for the End callback to complete
            Assert.True(SpinWait.SpinUntil(
                () =>
                {
                    Thread.Sleep(10);
                    return exportedItems.Count >= count;
                },
                TimeSpan.FromSeconds(1)));
        }

        private static void ValidateAspNetCoreActivity(Activity activityToValidate, string expectedHttpPath)
        {
            Assert.Equal(ActivityKind.Server, activityToValidate.Kind);
            Assert.Equal(HttpInListener.ActivitySourceName, activityToValidate.Source.Name);
            Assert.Equal(HttpInListener.Version.ToString(), activityToValidate.Source.Version);
            Assert.Equal(expectedHttpPath, activityToValidate.GetTagValue(SemanticConventions.AttributeHttpTarget) as string);
        }

        private void ConfigureTestServices(IServiceCollection services, int mode, ref List<Activity> exportedItems)
        {
            switch (mode)
            {
                case 1:
                    services.AddMvc(x => x.Filters.Add<ExceptionFilter1>());
                    break;
                case 2:
                    services.AddMvc(x => x.Filters.Add<ExceptionFilter1>());
                    services.AddMvc(x => x.Filters.Add<ExceptionFilter2>());
                    break;
                default:
                    break;
            }

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(x => x.RecordException = true)
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        private void AssertException(List<Activity> exportedItems)
        {
            Assert.Single(exportedItems);
            var activity = exportedItems[0];

            var exMessage = "something's wrong!";
            Assert.Equal("System.Exception", activity.Events.First().Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
            Assert.Equal(exMessage, activity.Events.First().Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);

            var status = activity.GetStatus();
            Assert.Equal(status, Status.Error.WithDescription(exMessage));

            ValidateAspNetCoreActivity(activity, "/api/error");
        }
    }
}
