using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using SeoZoomReader.BLL;
using SerpRankingAPI.Config;

namespace MySQLConfigurationAndSsh;

public static class MySqlDal
{
    //public static MySqlConnection connDbSeozoom => WebSiteConfiguration.WebsitesConfigs
    //    .SingleOrDefault(w => w.ShortSiteName == "DbSeozoom")?.MySql;

    public static MySqlConnection ConnSitoWp
    {
        get
        {
            if (WebSiteConfiguration.SelectedWebsite == null)
                throw new Exception("Devi selezionare un sito");

            return WebSiteConfiguration.SelectedWebsite?.MySql.Connection;
        }
    }

    public static MySqlConnection ConnSitoWpShortTimeout
    {
        get
        {
            if (WebSiteConfiguration.SelectedWebsite == null)
                throw new Exception("Devi selezionare un sito");

            return WebSiteConfiguration.SelectedWebsite?.MySql.ShortTimeoutConnection;
        }
    }

    private static void TruncateTable(string tablename, MySqlConnection conn)
    {
        ExecuteNonQuery(conn, $"TRUNCATE TABLE `{tablename}`");
    }

    public static void ExecuteNonQuery(MySqlConnection conn, string commandText)
    {
        var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn
        };
        conn.Open();

        try
        {
            var aff = cmd.ExecuteNonQuery();
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
            var connString = conn.ConnectionString + "AllowLoadLocalInfile = True";
            var bulkCopy = new MySqlBulkCopy(new MySqlConnection(connString))
            {
                DestinationTableName = destinationTableName,
                BulkCopyTimeout = 0
            };
            await bulkCopy.WriteToServerAsync(dataTable);

            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public static object ExecuteScalar(MySqlConnection conn, string commandText)
    {
        var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn
        };
        conn.Open();

        try
        {
            var aff = cmd.ExecuteScalar();
            return aff;
        }
        finally
        {
            conn.Close();
        }
    }

    public static DataTable ExecuteQuery(MySqlConnection conn, string commandText, IProgressManager progress = null)
    {
        var table = new DataTable();

        var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn,
            CommandTimeout = 60,
            //la connection timeout è a 15 secondi
        };

        var timer = new Stopwatch();
        timer.Start();
        progress?.ReportMessage("Open connection");
        conn.Open();
        //MySqlConnector.MySqlException: 'Couldn't connect to server'
        //EndOfStreamException: Expected to read 4 header bytes but only received 0.
        ReportProgress(progress, timer);

        progress?.ReportMessage("Open adapter");
        var adapter = new MySqlDataAdapter(cmd);
        ReportProgress(progress, timer);

        progress?.ReportMessage("Start adapter.Fill");
        adapter.Fill(table);
        ReportProgress(progress, timer);


        progress?.ReportMessage("Start executeReader");
        var p = cmd.ExecuteReader();
        ReportProgress(progress, timer);

        progress?.ReportMessage(Environment.NewLine);
        //});


        return table;
    }

    public static async Task<DataTable> ExecuteQueryAsync(MySqlConnection conn, string commandText,
        IProgressManager progress)
    {
        return await Task.Run(() =>
        {
            var table = new DataTable();

            using var cmd = new MySqlCommand
            {
                CommandText = commandText,
                Connection = conn,
                CommandTimeout = 60,
                //la connection timeout è a 15 secondi
            };

            var timer = new Stopwatch();
            timer.Start();
            progress?.ReportMessage("Open connection; conn.State = " + conn.State);

            conn.Open();
            //MySqlConnector.MySqlException: 'Couldn't connect to server'
            //EndOfStreamException: Expected to read 4 header bytes but only received 0.
            ReportProgress(progress, timer);

            progress?.ReportMessage("Open adapter");
            var adapter = new MySqlDataAdapter(cmd);
            ReportProgress(progress, timer);

            progress?.ReportMessage("Start adapter.Fill");
            adapter.Fill(table);
            ReportProgress(progress, timer);


            progress?.ReportMessage("Start executeReader");
            var p = cmd.ExecuteReaderAsync();
            ReportProgress(progress, timer);

            progress?.ReportMessage(Environment.NewLine);

            return table;
        });
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
            await conn.OpenAsync(cancellationToken);
            ReportProgress(progress, timer);

            progress?.ReportMessage("Executing query...");
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
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
        // Questo metodo esegue la query e inizia l'elaborazione dei record in modo "rolling",
        // senza attendere il completamento dell'intero risultato.
        // I record vengono mappati utilizzando la funzione rowMapper e elaborati man mano che vengono letti.

        await using var conn = new MySqlConnection(connectionString);
        await using var cmd = new MySqlCommand(commandText, conn);
        cmd.CommandTimeout = 180;

        var timer = Stopwatch.StartNew();

        //try
        //{
        progress?.ReportMessage("Opening connection...");
        await conn.OpenAsync(cancellationToken);
        ReportProgress(progress, timer);

        progress?.ReportMessage("Executing query...");
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        ReportProgress(progress, timer);

        while (await reader.ReadAsync(cancellationToken))
        {
            ReportProgress(progress, timer);
            yield return rowMapper(reader);
        }
        //}
        //catch (Exception ex)
        //{
        //    progress?.ReportMessage($"Error: {ex.Message}");
        //    throw;
        //}
        //finally
        //{
        timer.Stop();
        progress?.ReportMessage("Query execution completed.");
        //}
    }



    private static void ReportProgress(IProgressManager progress, Stopwatch timer)
    {
        progress?.ReportMessage($"Done in {timer.ElapsedMilliseconds}ms");
        timer.Restart();
    }

    public static MySqlDataReader ExecuteQuery2(MySqlConnection conn, string commandText)
    {
        var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn,
            CommandTimeout = 30,

        };

        //AllowZeroDateTime=true

        conn.Open();

        var p = cmd.ExecuteReader();

        return p;
    }

    public static string MatchWholeWord(string parola)
    {
        return $"'[[:<:]]{parola}[[:>:]]' ";
    }
}