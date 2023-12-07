// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Diagnostics;

namespace RouteTests.TestApplication;

/// <summary>
/// This observer captures all the available route information for a request.
/// This route information is used for generating a README file for analyzing
/// what information is available in different scenarios.
/// </summary>
internal sealed class RouteInfoDiagnosticObserver : IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    internal const string OnStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
    internal const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
    internal const string OnMvcBeforeActionEvent = "Microsoft.AspNetCore.Mvc.BeforeAction";

    private readonly List<IDisposable> listenerSubscriptions = new();
    private IDisposable? allSourcesSubscription;
    private long disposed;

    public RouteInfoDiagnosticObserver()
    {
        this.allSourcesSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == "Microsoft.AspNetCore")
        {
            var subscription = value.Subscribe(this);

            lock (this.listenerSubscriptions)
            {
                this.listenerSubscriptions.Add(subscription);
            }
        }
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        HttpContext? context;
        BeforeActionEventData? actionMethodEventData;
        RouteInfo? info;

        switch (value.Key)
        {
            case OnStartEvent:
                context = value.Value as HttpContext;
                Debug.Assert(context != null, "HttpContext was null");
                info = new RouteInfo();
                info.SetValues(context);
                RouteInfo.Current = info;
                break;
            case OnMvcBeforeActionEvent:
                actionMethodEventData = value.Value as BeforeActionEventData;
                Debug.Assert(actionMethodEventData != null, $"expected {nameof(BeforeActionEventData)}");
                RouteInfo.Current.SetValues(actionMethodEventData.HttpContext);
                RouteInfo.Current.SetValues(actionMethodEventData.ActionDescriptor);
                break;
            case OnStopEvent:
                context = value.Value as HttpContext;
                Debug.Assert(context != null, "HttpContext was null");
                RouteInfo.Current.SetValues(context);
                break;
            default:
                break;
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
        {
            return;
        }

        lock (this.listenerSubscriptions)
        {
            foreach (var listenerSubscription in this.listenerSubscriptions)
            {
                listenerSubscription?.Dispose();
            }

            this.listenerSubscriptions.Clear();
        }

        this.allSourcesSubscription?.Dispose();
        this.allSourcesSubscription = null;
    }
}
