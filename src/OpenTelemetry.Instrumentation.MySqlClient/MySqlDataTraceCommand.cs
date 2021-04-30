using MySql.Data.MySqlClient;

namespace OpenTelemetry.Instrumentation.MySqlClient
{
    /// <summary>
    /// Informations of current executing command.
    /// </summary>
    internal class MySqlDataTraceCommand
    {
        public MySqlConnectionStringBuilder ConnectionStringBuilder { get; set; }

        public string SqlText { get; set; }
    }
}
