using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connectionString = ""Server=(localdb)\\mssqllocaldb;Database=AssetHub;Trusted_Connection=True;MultipleActiveResultSets=true"";
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand(""SELECT Id, Name, IsActive, TenantId FROM AssetTemplates"", connection);
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine($""{reader[""Id""]} | {reader[""Name""]} | {reader[""IsActive""]} | {reader[""TenantId""]}"");
                }
            }
        }
    }
}
