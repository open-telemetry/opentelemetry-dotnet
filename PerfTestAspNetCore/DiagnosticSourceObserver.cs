using System.Diagnostics;

namespace PerfTestAspNetCore
{
    internal sealed class DiagnosticSourceSubscriber : IObserver<DiagnosticListener>
    {
        private static readonly HashSet<string> DiagnosticSourceEvents = new()
        {
            "Microsoft.AspNetCore.Hosting.HttpRequestIn",
            "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start",
            "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop",
            "Microsoft.AspNetCore.Mvc.BeforeAction",
            "Microsoft.AspNetCore.Diagnostics.UnhandledException",
            "Microsoft.AspNetCore.Hosting.UnhandledException",
        };

        private readonly Func<string, object?, object?, bool> isEnabled = (eventName, _, _)
            => DiagnosticSourceEvents.Contains(eventName);

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Microsoft.AspNetCore")
            {
                _ = value.Subscribe(new DiagnosticSourceListener(), isEnabled);
            }
        }
    }
}
