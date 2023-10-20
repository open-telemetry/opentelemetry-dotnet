using System.Diagnostics;

namespace PerfTestAspNetCore
{
    public class HttpTagHelper
    {
        public static string GetFlavorTagValueFromProtocol(string protocol)
        {
            switch (protocol)
            {
                case "HTTP/2":
                    return "2.0";

                case "HTTP/3":
                    return "3.0";

                case "HTTP/1.1":
                    return "1.1";

                default:
                    return protocol;
            }
        }

        public static ActivityStatusCode ResolveSpanStatusForHttpStatusCode(ActivityKind kind, int httpStatusCode)
        {
            var upperBound = kind == ActivityKind.Client ? 399 : 499;
            if (httpStatusCode >= 100 && httpStatusCode <= upperBound)
            {
                return ActivityStatusCode.Unset;
            }

            return ActivityStatusCode.Error;
        }
    }
}
