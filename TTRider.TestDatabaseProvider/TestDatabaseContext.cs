using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
//using System.ComponentModel.Design;
//using System.Configuration;
//using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TTRider.Test
{
    public interface ITestDatabaseContext : IDisposable
    {
        string Name { get; }

        string ConnectionString { get; }

        string DatabaseName { get; }

        void Initialize(Action<DbConnection> handler);

        Task InitializeAsync(Func<DbConnection, Task> handler);
    }

    public class TestDatabaseContext<T> : ITestDatabaseContext
        where T : DbConnection, new()
    {
        private static readonly string StateExt = ".TDBC_" + typeof(T).Name;

        private readonly List<T> connections = new List<T>();
        public const string DefaultConnectionString = "Data Source=(LocalDB)\\v11.0; Integrated Security=true;";
        private readonly string baseConnectionString;
        private readonly string stateFile;
        private readonly ILogger<TestDatabaseContext<T>> logger;

        public string Name { get; }

        public string ConnectionString { get; }

        public string DatabaseName { get; }

        public static ITestDatabaseContext Create(string serverConnectionString, Action<T> initialier = null)
        {
            return new TestDatabaseContext<T>(serverConnectionString, null, null, initialier);
        }
        public static ITestDatabaseContext Create(string serverConnectionString, ILoggerFactory loggerFactory, Action<T> initialier = null)
        {
            return new TestDatabaseContext<T>(serverConnectionString, null, loggerFactory, initialier);
        }
        public static ITestDatabaseContext Create(string serverConnectionString, string name, Action<T> initialier = null)
        {
            return new TestDatabaseContext<T>(serverConnectionString, name, new LoggerFactory(), initialier);
        }
        public static ITestDatabaseContext Create(string serverConnectionString, string name, ILoggerFactory loggerFactory, Action<T> initialier = null)
        {
            return new TestDatabaseContext<T>(serverConnectionString, name, loggerFactory, initialier);
        }

        private void ClearAllPools()
        {
            var m = typeof(T).GetRuntimeMethod("ClearAllPools", new Type[0]);

            if (m != null)
            {
                m.Invoke(null, null);
            }
        }

        private TestDatabaseContext(string serverConnectionString, string name, ILoggerFactory loggerFactory, Action<T> initialier = null)
        {
            this.logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<TestDatabaseContext<T>>();
            name = name ?? "";

            this.baseConnectionString = serverConnectionString;
            this.Name = name;

            this.DatabaseName = $"DB{name}_{Guid.NewGuid():N}";
            logger.LogDebug($"TDBC database: {DatabaseName}");
            logger.LogDebug($"TDBC master connection string: {baseConnectionString}");

            var connection = new T { ConnectionString = baseConnectionString };

            var existingDatabases = new DirectoryInfo(Path.GetTempPath()).EnumerateFiles("*" + StateExt).ToList();
            using (connection)
            {
                logger.LogDebug($"TDBC opening connection: {baseConnectionString}");
                connection.Open();

                try
                {
                    logger.LogDebug($"TDBC creating test database {DatabaseName}");
                    var command = connection.CreateCommand();
                    command.CommandText = $"CREATE DATABASE {DatabaseName}";
                    command.CommandType = CommandType.Text;
                    command.ExecuteNonQuery();
                    command.Dispose();
                }
                finally
                {
                    connection.Close();
                    connection.Dispose();
                }

                this.stateFile = Path.Combine(Path.GetTempPath(), $"TDBC{Guid.NewGuid():N}{StateExt}");
                File.WriteAllLines(stateFile, new[] { this.DatabaseName, stateFile });

                var csb = new SqlConnectionStringBuilder(this.baseConnectionString)
                {
                    InitialCatalog = DatabaseName
                };

                logger.LogDebug($"TDBC connection string: {csb.ConnectionString}");
                this.ConnectionString = csb.ConnectionString;
            }

            if (initialier != null)
            {
                logger.LogDebug($"TDBC initializing");
                using (var c = CreateConnection())
                {
                    c.Open();
                    initialier(c);
                }
                logger.LogDebug($"TDBC initialized");
            }

            DeleteDatabases(existingDatabases);
        }

        private void DeleteDatabases(IEnumerable<FileInfo> stateFiles)
        {

            Task.Run(() =>
            {

                foreach (var sf in stateFiles)
                {

                    try
                    {
                        if (sf.Exists)
                        {

                            var state = File.ReadAllLines(sf.FullName);

                            logger.LogDebug($"TDC deleting database {state[0]}");
                            var connection = new T { ConnectionString = this.baseConnectionString };
                            connection.Open();
                            var command = connection.CreateCommand();
                            command.CommandText = $"DROP DATABASE {state[0]}";
                            command.ExecuteNonQuery();
                            command.Dispose();
                            connection.Dispose();

                            sf.Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug($"Can't delete database now: {ex.Message}");
                    }
                }
            });
        }


        private void DeleteDatabase(string tdc)
        {
            if (string.IsNullOrWhiteSpace(tdc)) return;

            // let's try to delete database on the background.
            // if we fail, we will try again on the next run

            Task.Run(() =>
            {

                try
                {
                    if (File.Exists(tdc))
                    {
                        var state = File.ReadAllLines(tdc);

                        logger.LogDebug($"TDBC closing connections");
                        foreach (var sqlConnection in this.connections)
                        {
                            sqlConnection.Dispose();
                        }
                        ClearAllPools();

                        logger.LogDebug($"TDC deleting database {state[0]}");
                        var connection = new T { ConnectionString = this.baseConnectionString };
                        connection.Open();
                        var command = connection.CreateCommand();
                        command.CommandText = $"DROP DATABASE {state[0]}";
                        command.ExecuteNonQuery();
                        command.Dispose();
                        connection.Dispose();
                        ClearAllPools();

                        File.Delete(tdc);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug($"Can't delete database now: {ex.Message}");
                }
            });
        }


        public void Dispose()
        {
            DeleteDatabase(this.stateFile);
        }

        ~TestDatabaseContext()
        {
            DeleteDatabase(this.stateFile);
        }

        public T CreateConnection()
        {
            var c = new T { ConnectionString = this.ConnectionString };
            this.connections.Add(c);
            return c;
        }

        public void Initialize(Action<DbConnection> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            using (var connection = CreateConnection())
            {
                connection.Open();
                handler(connection);
            }
        }

        public async Task InitializeAsync(Func<DbConnection, Task> handler)
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