// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using TestApp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddMvc();

builder.Services.AddSingleton<HttpClient>();

builder.Services.AddSingleton(
    new CallbackMiddleware.CallbackMiddlewareImpl());

builder.Services.AddSingleton(
    new ActivityMiddleware.ActivityMiddlewareImpl());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseMiddleware<CallbackMiddleware>();

app.UseMiddleware<ActivityMiddleware>();

app.AddTestMiddleware();

app.Run();
