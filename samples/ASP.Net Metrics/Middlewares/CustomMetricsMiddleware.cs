using hellocs.MetricsShim;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace hellocs.Middlewares
{
    // Sadly, I didn't figure how to template the functions that provide data to the exporter
    public class MetricsMiddleware
    {
        // Handle to the next Middleware in the pipeline  
        private readonly RequestDelegate _next;
        // Handle to the metrics Exporter
        private readonly MetricShim _shim;
        private long responseTimeForCompleteRequest;
        public MetricsMiddleware(RequestDelegate next, MetricShim shim)
        {
            _next = next;
            _shim = shim;
        }

        public object HttpContext { get; private set; }

        public Task InvokeAsync(HttpContext context)
        {
            // Start the Timer using Stopwatch  
            var watch = new Stopwatch();
            watch.Start();
            context.Response.OnStarting(() =>
            {
                // Stop the timer information and calculate the time   
                writeRequestsToMetrics(context);
                watch.Stop();
                responseTimeForCompleteRequest = watch.ElapsedMilliseconds;
                // Add the Response time information to the metrics exporter
                writeRequestsTimeToMetrics(context);
                return Task.CompletedTask;
            });
            // Call the next delegate/middleware in the pipeline   
            return this._next(context);
        }

        /// <summary>
        /// This is a CUSTOM function made to fit with metrics views_requests_time view define in 
        /// appsettings.json.
        /// </summary>
        private void writeRequestsTimeToMetrics(HttpContext context)
        {
            try
            {
                View vue;
                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("route", context.Request.Path);
                dic.Add("status_code", context.Response.StatusCode.ToString());
                _shim.MetricsExp.Views.TryGetValue("req_time", out vue);

                vue.UpdateView(dic, Convert.ToInt32(responseTimeForCompleteRequest));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        /// <summary>
        /// This is a CUSTOM function made to fit with metrics views_requests_total view define in 
        /// appsettings.json.
        /// </summary>
        private void writeRequestsToMetrics(HttpContext context)
        {
            try
            {
                View vue;
                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("route", context.Request.Path);
                dic.Add("status_code", context.Response.StatusCode.ToString());
                _shim.MetricsExp.Views.TryGetValue("req_amount", out vue);

                vue.UpdateView(dic, 1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
