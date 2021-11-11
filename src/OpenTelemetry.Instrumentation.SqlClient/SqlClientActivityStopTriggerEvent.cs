namespace OpenTelemetry.Instrumentation.SqlClient
{
    /// <summary>
    /// Enumeration that defines kinds of events that trigger the stopping of the activity.
    /// </summary>
    public enum SqlClientActivityStopTriggerEvent
    {
        /// <summary>
        /// Denotes that the activity should stop on the WriteCommandAfter event.
        /// </summary>
        WriteCommandAfter,

        /// <summary>
        /// Denotes that the activity should stop on the WriteConnectionCloseAfter event.
        /// </summary>
        WriteConnectionCloseAfter,
    }
}
