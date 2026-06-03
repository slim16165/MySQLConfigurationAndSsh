using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using SeoZoomReader.BLL;
using SerpRankingAPI.Config;

namespace MySQLConfigurationAndSsh;

public static partial class MySqlDal
{
    public static MySqlConnection ConnSitoWp
    {
        get
        {
            if (WebSiteConfiguration.SelectedWebsite == null)
            {
                throw new Exception("Devi selezionare un sito");
            }

            return WebSiteConfiguration.SelectedWebsite.MySql.Connection;
        }
    }

    public static MySqlConnection ConnSitoWpShortTimeout
    {
        get
        {
            if (WebSiteConfiguration.SelectedWebsite == null)
            {
                throw new Exception("Devi selezionare un sito");
            }

            return WebSiteConfiguration.SelectedWebsite.MySql.ShortTimeoutConnection;
        }
    }

    private static void TruncateTable(string tablename, MySqlConnection conn)
    {
        ExecuteNonQuery(conn, $"TRUNCATE TABLE `{tablename}`");
    }

    public static void ExecuteNonQuery(MySqlConnection conn, string commandText)
    {
        using var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn
        };

        conn.Open();
        try
        {
            cmd.ExecuteNonQuery();
        }
        finally
        {
            conn.Close();
        }
    }

    public static async Task<bool> ExecuteBulkCopy(MySqlConnection conn, DataTable dataTable, string destinationTableName)
    {
        try
        {
            var connString = conn.ConnectionString + ";AllowLoadLocalInfile=true";
            await using var bulkConnection = new MySqlConnection(connString);
            await bulkConnection.OpenAsync().ConfigureAwait(false);

            var bulkCopy = new MySqlBulkCopy(bulkConnection)
            {
                DestinationTableName = destinationTableName,
                BulkCopyTimeout = 0
            };

            await bulkCopy.WriteToServerAsync(dataTable).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static object ExecuteScalar(MySqlConnection conn, string commandText)
    {
        using var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn
        };

        conn.Open();
        try
        {
            return cmd.ExecuteScalar();
        }
        finally
        {
            conn.Close();
        }
    }

    public static object ExecuteScalar(MySqlConnection conn, string commandText, MySqlParameterCollection parameters)
    {
        if (conn.State == ConnectionState.Closed)
        {
            conn.Open();
        }

        using var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn
        };

        foreach (MySqlParameter parameter in parameters)
        {
            cmd.Parameters.Add(parameter);
        }

        return cmd.ExecuteScalar();
    }

    public static object ExecuteScalar(MySqlCommand command)
    {
        return ExecuteScalar(command.Connection, command.CommandText, command.Parameters);
    }

    public static void ExecuteNonQueryWithOpenConnection(MySqlConnection conn, string commandText, MySqlParameterCollection parameters)
    {
        using var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn
        };

        foreach (MySqlParameter parameter in parameters)
        {
            cmd.Parameters.Add(parameter);
        }

        if (conn.State != ConnectionState.Open)
        {
            conn.Open();
        }

        try
        {
            cmd.ExecuteNonQuery();
        }
        finally
        {
            conn.Close();
        }
    }

    public static void ExecuteNonQueryWithOpenConnection(MySqlCommand command)
    {
        ExecuteNonQueryWithOpenConnection(command.Connection, command.CommandText, command.Parameters);
    }

    public static DataTable ExecuteQuery(MySqlConnection conn, string commandText, IProgressManager progress = null)
    {
        var table = new DataTable();
        using var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn,
            CommandTimeout = 60
        };

        var timer = Stopwatch.StartNew();
        progress?.ReportMessage("Open connection");
        conn.Open();
        ReportProgress(progress, timer);

        progress?.ReportMessage("Open adapter");
        var adapter = new MySqlDataAdapter(cmd);
        ReportProgress(progress, timer);

        progress?.ReportMessage("Start adapter.Fill");
        adapter.Fill(table);
        ReportProgress(progress, timer);

        progress?.ReportMessage(Environment.NewLine);
        return table;
    }

    public static async Task<DataTable> ExecuteQueryAsync(MySqlConnection conn, string commandText, IProgressManager progress)
    {
        return await Task.Run(() => ExecuteQuery(conn, commandText, progress)).ConfigureAwait(false);
    }

    public static async Task<DataTable> ExecuteQueryAsyncNew(string connectionString, string commandText,
        IProgressManager progress, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await using var cmd = new MySqlCommand(commandText, conn);
            cmd.CommandTimeout = 120;

            progress?.ReportMessage("Opening connection...");
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            ReportProgress(progress, timer);

            progress?.ReportMessage("Executing query...");
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            ReportProgress(progress, timer);

            var table = new DataTable();
            table.Load(reader);
            ReportProgress(progress, timer);

            return table;
        }
        catch (Exception ex)
        {
            progress?.ReportMessage($"Error: {ex.Message}");
            throw;
        }
        finally
        {
            timer.Stop();
            progress?.ReportMessage("Query execution completed.");
        }
    }

    public static async IAsyncEnumerable<T> ExecuteQueryWithStreamingAsync<T>(string connectionString, string commandText,
        Func<MySqlDataReader, T> rowMapper, IProgressManager progress, CancellationToken cancellationToken = default)
    {
        await using var conn = new MySqlConnection(connectionString);
        await using var cmd = new MySqlCommand(commandText, conn);
        cmd.CommandTimeout = 180;

        var timer = Stopwatch.StartNew();

        progress?.ReportMessage("Opening connection...");
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        ReportProgress(progress, timer);

        progress?.ReportMessage("Executing query...");
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        ReportProgress(progress, timer);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ReportProgress(progress, timer);
            yield return rowMapper(reader);
        }

        timer.Stop();
        progress?.ReportMessage("Query execution completed.");
    }

    public static MySqlDataReader ExecuteQuery2(MySqlConnection conn, string commandText)
    {
        var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn,
            CommandTimeout = 30
        };

        conn.Open();
        return cmd.ExecuteReader();
    }

    public static MySqlDataReader ExecuteQuery2(MySqlCommand cmd)
    {
        cmd.Connection.Open();
        return cmd.ExecuteReader();
    }

    public static string MatchWholeWord(string parola)
    {
        return $"'[[:<:]]{parola}[[:>:]]' ";
    }

    private static void ReportProgress(IProgressManager progress, Stopwatch timer)
    {
        progress?.ReportMessage($"Done in {timer.ElapsedMilliseconds}ms");
        timer.Restart();
    }
}
