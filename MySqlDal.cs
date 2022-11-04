using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using GoogleDocToPdfFramework.MySql.Config;
using MySqlConnector;

namespace MySQLConfigurationAndSsh
{
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

                return WebSiteConfiguration.SelectedWebsite?.MySql;
            }
        }

        public static MySqlConnection ConnSitoWpShortTimeout
        {
            get
            {
                if (WebSiteConfiguration.SelectedWebsite == null)
                    throw new Exception("Devi selezionare un sito");

                return WebSiteConfiguration.SelectedWebsite?.MySql.ShortTimeout;
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

        public static DataTable ExecuteQuery(MySqlConnection conn, string commandText, IProgress<string> progress = null)
        {
            var table = new DataTable();
            
            //var task = Task.Factory.StartNew(() =>
            //{
                var cmd = new MySqlCommand
                {
                    CommandText = commandText,
                    Connection = conn,
                    CommandTimeout = 60,
                    //la connection timeout è a 15 secondi
                };

                var timer = new Stopwatch();
                timer.Start();
                progress?.Report("Open connection");
                conn.Open();
                //MySqlConnector.MySqlException: 'Couldn't connect to server'
                //EndOfStreamException: Expected to read 4 header bytes but only received 0.
                ReportProgress(progress, timer);

                progress?.Report("Open adapter");
                var adapter = new MySqlDataAdapter(cmd);
                ReportProgress(progress, timer);

                progress?.Report("Start adapter.Fill");
                adapter.Fill(table);
                ReportProgress(progress, timer);


                progress?.Report("Start executeReader");
                var p = cmd.ExecuteReader();
                ReportProgress(progress, timer);

                progress?.Report(Environment.NewLine);
            //});

            
            return table;
        }

        public async static Task<DataTable> ExecuteQueryAsync(MySqlConnection conn, string commandText,
            IProgress<string> progress = null)
        {
            var task = Task.Factory.StartNew(() =>
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
                progress?.Report("Open connection");
                conn.Open();
                //MySqlConnector.MySqlException: 'Couldn't connect to server'
                //EndOfStreamException: Expected to read 4 header bytes but only received 0.
                ReportProgress(progress, timer);

                progress?.Report("Open adapter");
                var adapter = new MySqlDataAdapter(cmd);
                ReportProgress(progress, timer);

                progress?.Report("Start adapter.Fill");
                adapter.Fill(table);
                ReportProgress(progress, timer);


                progress?.Report("Start executeReader");
                var p = cmd.ExecuteReader();
                ReportProgress(progress, timer);

                progress?.Report(Environment.NewLine);

                return table;
            });

            return await task;

        }

        private static void ReportProgress(IProgress<string> progress, Stopwatch timer)
        {
            progress?.Report($"Done in {timer.ElapsedMilliseconds}ms");
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
}