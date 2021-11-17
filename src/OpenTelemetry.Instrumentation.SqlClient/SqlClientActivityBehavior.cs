namespace OpenTelemetry.Instrumentation.SqlClient
{
    /// <summary>
    /// Enumeration that defines kinds of events that trigger the stopping of the activity.
    /// </summary>
    public enum SqlClientActivityBehavior
    {
        /// <summary>
        /// Denotes that the activity should start before executing the command and stop after executing.
        /// </summary>
        Command,

        /// <summary>
        /// Denotes that the activity should start before opening the connection and  stop after closing the connection.
        /// </summary>
        Connection,
    }
}
