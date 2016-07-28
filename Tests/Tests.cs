using System;
using System.Data.SqlClient;
using Xunit;
using TTRider.Test;

namespace Tests
{
    public class Tests
    {
        [Fact]
        public void TestSuccess()
        {
            using (var context = TestDatabaseContext.Create("test", cnn =>
            {
                var command = new SqlCommand("SELECT * FROM sys.objects", cnn);
                command.ExecuteNonQuery();
            }))
            {
                context.Initialize(cnn =>
                {
                    var command = new SqlCommand("SELECT * FROM sys.tables", cnn);
                    command.ExecuteNonQuery();

                });


            }



        }
    }
}
