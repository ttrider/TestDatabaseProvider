using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;

namespace TTRider.Test
{
    public class TestDatabaseContext : IDisposable
    {
        private readonly List<SqlConnection> connections = new List<SqlConnection>();
        private readonly string databaseName;


        public static TestDatabaseContext Create(Action<SqlConnection> initialier = null)
        {
            return new TestDatabaseContext("(LocalDB)\\v11.0", "TestDatabaseContext", null, initialier);
        }

        public static TestDatabaseContext Create(string key, Action<SqlConnection> initialier = null)
        {
            return new TestDatabaseContext("(LocalDB)\\v11.0", key, ".\\",initialier);
        }

        public static TestDatabaseContext Create(string server, string key, string path = null, Action<SqlConnection> initialier = null)
        {
            return new TestDatabaseContext(server, key, path, initialier);
        }

        public string Name { get; }

        public string ConnectionString { get; }


        TestDatabaseContext(string datasource, string id, string path=".\\", Action<SqlConnection> initialier = null)
        {
            this.Name = id;
            databaseName = $"DB{id}_{Guid.NewGuid().ToString("N")}";

            var dataFile = Path.GetFullPath($"{path}{databaseName}_data.mdf");
            var logFile = Path.GetFullPath($"{path}{databaseName}_log.ldf");

            var connection = new SqlConnection($"server={datasource}");
            using (connection)
            {
                connection.Open();

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
                    databaseName, dataFile, logFile);

                var command = new SqlCommand(sql, connection);
                command.ExecuteNonQuery();
                connection.Close();

                this.ConnectionString = $"Data Source={datasource};Integrated Security=True;Connect Timeout=30;Initial Catalog={databaseName};";
            }

            if (initialier != null)
            {
                this.Initialize(initialier);
            }
        }

        public void Dispose()
        {
            foreach (var sqlConnection in this.connections)
            {
                sqlConnection.Dispose();
            }
            SqlConnection.ClearAllPools();
            var connection = new SqlConnection(@"server=(LocalDB)\v11.0");
            connection.Open();
            connection.ChangeDatabase("master");


            string sql = $"DROP DATABASE [{this.databaseName}];";
            var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
            connection.Dispose();
            SqlConnection.ClearAllPools();
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