﻿using System;
using System.Data;
using System.Threading.Tasks;
using MySQLConfigurationAndSsh.Config;
using MySqlConnector;

namespace MySQLConfigurationAndSsh;

public static partial class MySqlDal
{
    //public static MySqlConnection connDbSeozoom => GenericMySQLConfiguration.WebsitesConfigs
    //    .SingleOrDefault(w => w.ShortSiteName == "DbSeozoom")?.MySql;

    public static MySqlConnection ConnSitoWp
    {
        get
        {
            if (GenericMySQLConfigurationNew.Instance.WebsitesConfigs == null)
                throw new Exception("Devi selezionare un sito");

            return GenericMySQLConfigurationNew.Instance.SelectedWebsite?.MySql;
        }
    }

    public static MySqlConnection ConnSitoWpShortTimeout
    {
        get
        {
            if (GenericMySQLConfigurationNew.Instance.SelectedWebsite == null)
                throw new Exception("Devi selezionare un sito");

            return GenericMySQLConfigurationNew.Instance.SelectedWebsite?.MySql.ShortTimeout;
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

    public static DataTable ExecuteQuery(MySqlConnection conn, string commandText)
    {
        var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn,
            CommandTimeout = 60,

        };
        conn.Open();

        var table = new DataTable();
        var adapter = new MySqlDataAdapter(cmd);
        adapter.Fill(table);

        var p = cmd.ExecuteReader();

        return table;
    }

    public static object ExecuteScalar(MySqlConnection conn, string commandText, MySqlParameterCollection parameters)
    {
        if(conn.State == ConnectionState.Closed)
            conn.Open();

        using var cmd = new MySqlCommand
        {
            CommandText = commandText,
            Connection = conn
        };
        foreach (MySqlParameter parameter in parameters)
        {
            cmd.Parameters.Add(parameter);
        }

        // Assuming the connection is already open
        try
        {
            return cmd.ExecuteScalar();
        }
        catch
        {
            // Handle possible exceptions such as connection issues, SQL errors, etc.
            throw;
        }
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

        // It's optional to check if the connection State is Open here since we're assuming it
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

    public static MySqlDataReader ExecuteQuery2(MySqlCommand cmd)
    {
        cmd.Connection.Open();

        var p = cmd.ExecuteReader();

        return p;
    }

    public static string MatchWholeWord(string parola)
    {
        return $"'[[:<:]]{parola}[[:>:]]' ";
    }

    public static void ExecuteNonQueryWithOpenConnection(MySqlCommand command)
    {
        ExecuteNonQueryWithOpenConnection(command.Connection, command.CommandText, command.Parameters);
    }

    public static object ExecuteScalar(MySqlCommand command)
    {
        return ExecuteScalar(command.Connection, command.CommandText, command.Parameters);
    }
}