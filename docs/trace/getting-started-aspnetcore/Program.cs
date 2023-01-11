// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var appBuilder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry with tracing and auto-start.
appBuilder.Services.AddOpenTelemetry()
    .ConfigureResource(builder => builder
        .AddService(serviceName: "OTel.NET Getting Started"))
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .StartWithHost();

// Add services to the container.
appBuilder.Services.AddRazorPages();

var app = appBuilder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
