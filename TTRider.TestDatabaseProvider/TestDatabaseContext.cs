using System;
using System.Collections.Generic;
using System.Data.SqlClient;
//using System.ComponentModel.Design;
//using System.Configuration;
//using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;

namespace TTRider.Test
{
    public class TestDatabaseContext : IDisposable
    {
        private readonly List<SqlConnection> connections = new List<SqlConnection>();
       

        public static TestDatabaseContext Create(Action<SqlConnection> initialier = null)
        {
            return new TestDatabaseContext("(LocalDB)\\v11.0", "TestDatabaseContext", null, initialier);
        }

        public static TestDatabaseContext Create(string key, Action<SqlConnection> initialier = null)
        {
            return new TestDatabaseContext("(LocalDB)\\v11.0", key, ".\\", initialier);
        }

        public static TestDatabaseContext Create(string server, string key, string path = null, Action<SqlConnection> initialier = null)
        {
            return new TestDatabaseContext(server, key, path, initialier);
        }

        public string Name { get; }

        public string ConnectionString { get; }

        public string DatabaseName { get; private set; }

        private string dataSource;
        private string stateFile;
        

        TestDatabaseContext(string datasource, string id, string path = ".\\", Action<SqlConnection> initialier = null)
        {
            this.Name = id;

            this.DatabaseName = $"DB{id}_{Guid.NewGuid().ToString("N")}";
            this.dataSource = datasource;
            var dataFile = Path.GetFullPath($"{path}{DatabaseName}_data.mdf");
            var logFile = Path.GetFullPath($"{path}{DatabaseName}_log.ldf");

            var connection = new SqlConnection($"server={datasource}");
            using (connection)
            {
                connection.Open();

                try
                {
                    string sql = string.Format(@"
                        CREATE DATABASE
                            [{0}]
                        ON PRIMARY (
                           NAME={0}_data,
                           FILENAME = '{1}'
                        )
                        LOG ON (
                            NAME={0}_log,
                            FILENAME = '{2}'
                        )",
                    DatabaseName, dataFile, logFile);

                    var command = new SqlCommand(sql, connection);
                    command.ExecuteNonQuery();
                }
                finally
                {
                    connection.Close();
                    connection.Dispose();
                }



                foreach (var tdc in new DirectoryInfo(Path.GetTempPath()).EnumerateFiles("*.TestDatabaseContext"))
                {
                    DeleteDatabase(tdc.FullName);
                }

                this.stateFile = Path.Combine(Path.GetTempPath(), $"TSC{Guid.NewGuid().ToString("N")}.TestDatabaseContext");
                File.WriteAllLines(stateFile,new [] {this.DatabaseName, stateFile});

                this.ConnectionString = $"Data Source={datasource};Integrated Security=True;Connect Timeout=30;Initial Catalog={DatabaseName};";
            }

            if (initialier != null)
            {
                this.Initialize(initialier);
            }
        }

        private void DeleteDatabase(string tdc)
        {
            if (string.IsNullOrWhiteSpace(tdc)) return;
            try
            {
                if (File.Exists(tdc))
                {
                    var state = File.ReadAllLines(tdc);

                    foreach (var sqlConnection in this.connections)
                    {
                        sqlConnection.Dispose();
                    }
                    SqlConnection.ClearAllPools();

                    var csb = new SqlConnectionStringBuilder(this.ConnectionString) {InitialCatalog = "master"};

                    var connection = new SqlConnection(csb.ConnectionString);
                    connection.Open();
                    connection.ChangeDatabase("master");


                    string sql = $"DROP DATABASE [{state[0]}];";
                    var command = new SqlCommand(sql, connection);
                    command.ExecuteNonQuery();
                    connection.Dispose();
                    SqlConnection.ClearAllPools();

                    File.Delete(tdc);
                }
            }
            finally { }
        }


        public void Dispose()
        {
            DeleteDatabase(this.stateFile);
        }

        ~TestDatabaseContext()
        {
            DeleteDatabase(this.stateFile);
        }

        public SqlConnection CreateConnection()
        {
            var c = new SqlConnection(this.ConnectionString);
            this.connections.Add(c);
            return c;
        }

        public void Initialize(Action<SqlConnection> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            using (var connection = CreateConnection())
            {
                connection.Open();
                handler(connection);
            }
        }

        public async Task InitializeAsync(Func<SqlConnection, Task> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                await handler(connection);
            }
        }
    }
}