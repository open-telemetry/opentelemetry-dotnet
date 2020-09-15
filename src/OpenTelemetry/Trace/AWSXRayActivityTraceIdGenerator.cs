using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace OpenTelemetry.Trace
{
    public static class AWSXRayActivityTraceIdGenerator
    {
        private const int RandomNumberHexDigits = 24; // 96 bits

        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private const long MicrosecondPerSecond = TimeSpan.TicksPerSecond / TicksPerMicrosecond;

        private static readonly DateTime EpochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly long UnixEpochMicroseconds = EpochStart.Ticks / TicksPerMicrosecond;
        private static readonly Random Global = new Random();
        private static readonly ThreadLocal<Random> Local = new ThreadLocal<Random>(() =>
        {
            int seed;
            lock (Global)
            {
                seed = Global.Next();
            }

            return new Random(seed);
        });

        /// <summary>
        /// Replace the trace id of root activity.
        /// </summary>
        /// <param name="builder">Instance of <see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/>.</returns>
        public static TracerProviderBuilder AddXRayActivityTraceIdGenerator(this TracerProviderBuilder builder)
        {
            var awsXRayActivityListener = new ActivityListener
            {
                ActivityStarted = (activity) =>
                {
                    // Replace every root activity's trace id with X-Ray compatiable trace id
                    if (string.IsNullOrEmpty(activity.ParentId))
                    {
                        var awsXRayTraceId = GenerateAWSXRayCompatiableActivityTraceId();

                        // Root node's parent id is no longer null, which will fail the sampler checker
                        // 00-traceid-0000000000000000-00
                        activity.SetParentId(awsXRayTraceId, default, activity.ActivityTraceFlags);
                    }
                },

                ShouldListenTo = (_) => true,
            };

            ActivitySource.AddActivityListener(awsXRayActivityListener);

            return builder;
        }

        public static ActivityTraceId GenerateAWSXRayCompatiableActivityTraceId()
        {
            var epoch = (int)DateTime.UtcNow.ToUnixTimeSeconds(); // first 8 digit as time stamp

            var randomNumber = GenerateHexNumber(RandomNumberHexDigits); // remaining 24 random digit

            return ActivityTraceId.CreateFromString(string.Concat(epoch.ToString("x", CultureInfo.InvariantCulture), randomNumber).AsSpan());
        }

        /// <summary>
        /// Convert a given time to Unix time which is the number of seconds since 1st January 1970, 00:00:00 UTC.
        /// </summary>
        /// <param name="date">.Net representation of time.</param>
        /// <returns>The number of seconds elapsed since 1970-01-01 00:00:00 UTC. The value is expressed in whole and fractional seconds with resolution of microsecond.</returns>
        private static decimal ToUnixTimeSeconds(this DateTime date)
        {
            long microseconds = date.Ticks / TicksPerMicrosecond;
            long microsecondsSinceEpoch = microseconds - UnixEpochMicroseconds;
            return (decimal)microsecondsSinceEpoch / MicrosecondPerSecond;
        }

        /// <summary>
        /// Generate a random 24-digit hex number.
        /// </summary>
        /// <param name="digits">Digits of the hex number.</param>
        /// <returns>The generated hex number.</returns>
        private static string GenerateHexNumber(int digits)
        {
            if (digits < 0)
            {
                throw new ArgumentException("Length can't be a negative number.", "digits");
            }

            byte[] bytes = new byte[digits / 2];
            NextBytes(bytes);
            string hexNumber = string.Concat(bytes.Select(x => x.ToString("x2", CultureInfo.InvariantCulture)).ToArray());
            if (digits % 2 != 0)
            {
                hexNumber += Next(16).ToString("x", CultureInfo.InvariantCulture);
            }

            return hexNumber;
        }

        /// <summary>
        /// Fills the elements of a specified array of bytes with random numbers.
        /// </summary>
        /// <param name="buffer">An array of bytes to contain random numbers.</param>
        private static void NextBytes(byte[] buffer)
        {
            Local.Value.NextBytes(buffer);
        }

        /// <summary>
        /// Returns a non-negative random integer that is less than the specified maximum.
        /// </summary>
        /// <param name="maxValue">Max value of the random integer.</param>
        /// <returns>A 32-bit signed integer that is greater than or equal to 0, and less than maxValue.</returns>
        private static int Next(int maxValue)
        {
            return Local.Value.Next(maxValue);
        }
    }
}
