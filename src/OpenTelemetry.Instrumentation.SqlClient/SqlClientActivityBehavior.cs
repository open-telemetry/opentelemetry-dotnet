namespace OpenTelemetry.Instrumentation.SqlClient
{
    /// <summary>
    /// Enumeration that defines kinds of events that trigger the stopping of the activity.
    /// </summary>
    public enum SqlClientActivityBehavior
    {
        /// <summary>
        /// Denotes that the activity should stop on the WriteCommandAfter event.
        /// </summary>
        Command,

        /// <summary>
        /// Denotes that the activity should stop on the WriteConnectionCloseAfter event.
        /// </summary>
        Connection,
    }
}
