using System.Data;
using System.Threading.Tasks;
using MySqlConnector;

namespace MySQLConfigurationAndSsh;

public static partial class MySqlDal
{
    // Esempio di esecuzione asincrona di un ExecuteScalar
    public static async Task<object> ExecuteScalarAsync(MySqlCommand command)
    {
        // command.Connection dovrebbe essere già impostata
        if (command.Connection.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync().ConfigureAwait(false);
        }

        try
        {
            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return result;
        }
        finally
        {
            command.Connection.Close();
        }
    }

    public static async Task<int> ExecuteNonQueryAsync(MySqlCommand command)
    {
        if (command.Connection.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync().ConfigureAwait(false);
        }

        try
        {
            int affected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            return affected;
        }
        finally
        {
            command.Connection.Close();
        }
    }

    /// <summary>
    /// Esegue un ExecuteReader e restituisce il DataReader già aperto.
    /// Sarà il chiamante a dover fare reader.Close() e/o using.
    /// </summary>
    public static async Task<MySqlDataReader> ExecuteReaderAsync(MySqlCommand command)
    {
        if (command.Connection.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync().ConfigureAwait(false);
        }

        // Non chiudiamo la connessione qui dentro, perché
        // il DataReader la richiede aperta finché non ha finito.
        // Sarà cura del chiamante "using var reader = ..." e
        // in seguito "command.Connection.Close()".
        var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection)
                                  .ConfigureAwait(false);
        return reader;
    }

    /// <summary>
    /// Esegue un NonQuery con connessione "Open" e parametri preimpostati, poi chiude.
    /// </summary>
    public static async Task ExecuteNonQueryWithOpenConnectionAsync(MySqlCommand command)
    {
        if (command.Connection.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync().ConfigureAwait(false);
        }

        try
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            command.Connection.Close();
        }
    }

    // Esempio di override: con passaggio di conn e commandText
    public static async Task ExecuteNonQueryWithOpenConnectionAsync(MySqlConnection conn, string commandText, MySqlParameterCollection parameters)
    {
        await using var cmd = new MySqlCommand(commandText, conn);
        foreach (MySqlParameter parameter in parameters)
        {
            cmd.Parameters.Add(parameter);
        }

        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync().ConfigureAwait(false);

        try
        {
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            conn.Close();
        }
    }

    // Esempio di ExecuteScalar con passaggio di conn e stringa 
    public static async Task<object> ExecuteScalarAsync(MySqlConnection conn, string commandText)
    {
        await using var cmd = new MySqlCommand(commandText, conn);
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync().ConfigureAwait(false);

        try
        {
            return await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        }
        finally
        {
            conn.Close();
        }
    }

    // Esempio: l'implementazione asincrona del BulkCopy
    public static async Task<bool> ExecuteBulkCopyAsync(MySqlConnection conn, DataTable dataTable, string destinationTableName)
    {
        try
        {
            var connString = conn.ConnectionString + ";AllowLoadLocalInfile=true";
            await using var bulkConn = new MySqlConnection(connString);
            await bulkConn.OpenAsync().ConfigureAwait(false);

            var bulkCopy = new MySqlBulkCopy(bulkConn)
            {
                DestinationTableName = destinationTableName,
                BulkCopyTimeout = 0 // infinito
            };

            await bulkCopy.WriteToServerAsync(dataTable).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}