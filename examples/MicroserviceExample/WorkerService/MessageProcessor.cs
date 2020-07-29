using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WorkerService
{
    public class MessageProcessor : IDisposable
    {
        private static readonly ActivitySource ActivitySource = new ActivitySource(nameof(MessageProcessor));
        private static readonly ITextFormat TextFormat = new TraceContextFormat();

        private readonly TracerProvider tracerProvider;

        public MessageProcessor()
        {
            this.tracerProvider = Sdk.CreateTracerProvider((builder) =>
            {
                builder
                    .AddActivitySource(nameof(MessageProcessor))
                    .UseZipkinExporter(b =>
                    {
                        var zipkinHostName = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
                        b.ServiceName = nameof(WorkerService);
                        b.Endpoint = new Uri($"http://{zipkinHostName}:9411/api/v2/spans");
                    });
            });
        }

        public void Dispose()
        {
            this.tracerProvider.Dispose();
        }

        public async Task ProcessMessage(BasicDeliverEventArgs ea)
        {
            var activityName = $"{ea.RoutingKey} receive";

            var parentContext = TextFormat.Extract(ea.BasicProperties, ExtractTraceContextFromBasicProperties);

            using (var activity = ActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext))
            {
                try
                {
                    var message = Encoding.UTF8.GetString(ea.Body.Span);

                    activity.AddTag("message", message);

                    // Simulate some work
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        private static IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            try
            {
                if (props.Headers.TryGetValue(key, out object value))
                {
                    var bytes = value as byte[];
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to extract trace context: {ex}");
            }

            return Enumerable.Empty<string>();
        }
    }
}
