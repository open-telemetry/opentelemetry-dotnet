using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PerfTestAspNetCore
{
    public static class ActivityExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? GetTagValue(this Activity activity, string tagName)
        {
            Debug.Assert(activity != null, "Activity should not be null");

            foreach (ref readonly var tag in activity.EnumerateTagObjects())
            {
                if (tag.Key == tagName)
                {
                    return tag.Value;
                }
            }

            return null;
        }
    }
}
