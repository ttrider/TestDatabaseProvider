using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
//using System.ComponentModel.Design;
//using System.Configuration;
//using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TTRider.Test
{
    //public class TestDatabaseContext : IDisposable
    //{
    //    private readonly List<SqlConnection> connections = new List<SqlConnection>();
    //    public const string DefaultConnectionString = "Data Source=(LocalDB)\\v11.0; Integrated Security=true;";
    //    private readonly string baseConnectionString;
    //    private readonly string stateFile;
    //    private readonly ILogger<TestDatabaseContext> logger;



    //    public static TestDatabaseContext Create(Action<SqlConnection> initialier = null)
    //    {
    //        return new TestDatabaseContext(DefaultConnectionString, null, ".\\", initialier);
    //    }

    //    public static TestDatabaseContext Create(string key, Action<SqlConnection> initialier = null)
    //    {
    //        return new TestDatabaseContext(DefaultConnectionString, key, ".\\", initialier);
    //    }

    //    public static TestDatabaseContext Create(string server, string key, string path = null, Action<SqlConnection> initialier = null)
    //    {
    //        return new TestDatabaseContext(server, key, path, initialier);
    //    }

    //    public string Name { get; }

    //    public string ConnectionString { get; }

    //    public string DatabaseName { get; }



    //    TestDatabaseContext(string connectionString, string id, string path = ".\\", Action<SqlConnection> initialier = null)
    //    {
    //        this.logger = new LoggerFactory().CreateLogger<TestDatabaseContext>();

    //        this.baseConnectionString = connectionString;
    //        this.Name = id;

    //        this.DatabaseName = $"DB{id}_{Guid.NewGuid():N}";
    //        logger.LogDebug($"TDC database: {DatabaseName}");
    //        var dataFile = Path.GetFullPath($"{path}{DatabaseName}_data.mdf");
    //        logger.LogDebug($"TDC mdf file: {dataFile}");
    //        var logFile = Path.GetFullPath($"{path}{DatabaseName}_log.ldf");
    //        logger.LogDebug($"TDC ldf file: {dataFile}");

    //        logger.LogDebug($"TDC master connection string: {connectionString}");
    //        var connection = new SqlConnection(connectionString);
    //        using (connection)
    //        {
    //            logger.LogDebug($"TDC opening connection: {connectionString}");
    //            connection.Open();

    //            try
    //            {
    //                string sql = string.Format(@"
    //                    CREATE DATABASE
    //                        [{0}]
    //                    ON PRIMARY (
    //                       NAME={0}_data,
    //                       FILENAME = '{1}'
    //                    )
    //                    LOG ON (
    //                        NAME={0}_log,
    //                        FILENAME = '{2}'
    //                    )",
    //                DatabaseName, dataFile, logFile);

    //                logger.LogDebug($"TDC creating test database {DatabaseName}");
    //                var command = new SqlCommand(sql, connection);
    //                command.ExecuteNonQuery();
    //            }
    //            finally
    //            {
    //                connection.Close();
    //                connection.Dispose();
    //            }

    //            foreach (var tdc in new DirectoryInfo(Path.GetTempPath()).EnumerateFiles("*.TestDatabaseContext"))
    //            {
    //                 DeleteDatabase(tdc.FullName);
    //            }

    //            this.stateFile = Path.Combine(Path.GetTempPath(), $"TSC{Guid.NewGuid():N}.TestDatabaseContext");
    //            File.WriteAllLines(stateFile, new[] { this.DatabaseName, stateFile });

    //            var csb = new SqlConnectionStringBuilder(this.baseConnectionString)
    //            {
    //                InitialCatalog = DatabaseName
    //            };

    //            logger.LogDebug($"TDC connection string: {csb.ConnectionString}");
    //            this.ConnectionString = csb.ConnectionString;
    //        }

    //        if (initialier != null)
    //        {
    //            logger.LogDebug($"TDC initializing");
    //            this.Initialize(initialier);
    //            logger.LogDebug($"TDC initialized");
    //        }
    //    }

    //    private void DeleteDatabase(string tdc)
    //    {
    //        if (string.IsNullOrWhiteSpace(tdc)) return;
    //        try
    //        {
    //            if (File.Exists(tdc))
    //            {
    //                var state = File.ReadAllLines(tdc);

    //                logger.LogDebug($"TDC closing connections");
    //                foreach (var sqlConnection in this.connections)
    //                {
    //                    sqlConnection.Dispose();
    //                }
    //                SqlConnection.ClearAllPools();

    //                logger.LogDebug($"TDC deleting database {state[0]}");
    //                var connection = new SqlConnection(this.baseConnectionString);
    //                connection.Open();
    //                connection.ChangeDatabase("master");

    //                string sql = $"IF EXISTS(SELECT name FROM sys.databases WHERE name = '{state[0]}') DROP DATABASE [{state[0]}];";
    //                var command = new SqlCommand(sql, connection);
    //                command.CommandTimeout = 1000;
    //                command.ExecuteNonQuery();
    //                connection.Dispose();
    //                SqlConnection.ClearAllPools();

    //                File.Delete(tdc);
    //            }
    //        }
    //        catch
    //        {
    //            // ignored
    //        }
    //    }


    //    public void Dispose()
    //    {
    //        //DeleteDatabase(this.stateFile);
    //    }

    //    ~TestDatabaseContext()
    //    {
    //        //DeleteDatabase(this.stateFile);
    //    }

    //    public SqlConnection CreateConnection()
    //    {
    //        var c = new SqlConnection(this.ConnectionString);
    //        this.connections.Add(c);
    //        return c;
    //    }

    //    public void Initialize(Action<SqlConnection> handler)
    //    {
    //        if (handler == null) throw new ArgumentNullException(nameof(handler));
    //        using (var connection = CreateConnection())
    //        {
    //            connection.Open();
    //            handler(connection);
    //        }
    //    }

    //    public async Task InitializeAsync(Func<SqlConnection, Task> handler)
    //    {
    //        if (handler == null) throw new ArgumentNullException(nameof(handler));
    //        using (var connection = CreateConnection())
    //        {
    //            await connection.OpenAsync();
    //            await handler(connection);
    //        }
    //    }
    //}


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

        public static ITestDatabaseContext Create(string serverConnectionString, Action<DbConnection> initialier = null)
        {
            return new TestDatabaseContext<T>(serverConnectionString, null, null, initialier);
        }
        public static ITestDatabaseContext Create(string serverConnectionString, ILoggerFactory loggerFactory, Action<DbConnection> initialier = null)
        {
            return new TestDatabaseContext<T>(serverConnectionString, null, loggerFactory, initialier);
        }
        public static ITestDatabaseContext Create(string serverConnectionString, string name, Action<DbConnection> initialier = null)
        {
            return new TestDatabaseContext<T>(serverConnectionString, name, new LoggerFactory(), initialier);
        }
        public static ITestDatabaseContext Create(string serverConnectionString, string name, ILoggerFactory loggerFactory, Action<DbConnection> initialier = null)
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

        private TestDatabaseContext(string serverConnectionString, string name, ILoggerFactory loggerFactory, Action<DbConnection> initialier = null)
        {
            this.logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<TestDatabaseContext<T>>();
            name = name ?? "";

            this.baseConnectionString = serverConnectionString;
            this.Name = name;

            this.DatabaseName = $"DB{name}_{Guid.NewGuid():N}";
            logger.LogDebug($"TDBC database: {DatabaseName}");
            logger.LogDebug($"TDBC master connection string: {baseConnectionString}");

            var connection = new T { ConnectionString = baseConnectionString };
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

                foreach (var tdc in new DirectoryInfo(Path.GetTempPath()).EnumerateFiles("*" + StateExt))
                {
                    DeleteDatabase(tdc.FullName);
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
                this.Initialize(initialier);
                logger.LogDebug($"TDBC initialized");
            }
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