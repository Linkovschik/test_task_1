using System;
using System.Data.SqlClient;
using System.IO;

namespace test_task_1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine("Тестовые коммиты! Никогда с вижаком не работал на гитхаб");
            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\Super\source\repos\test_task_1\test_task_1\Database1.mdf;Integrated Security=True";
            string queryString = "INSERT dbo.testResult VALUES ('forth', 'sdfhh','ffffffffff');";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(
                    queryString, connection);
                connection.Open();
                command.ExecuteReader();
                //using (SqlDataReader reader = command.ExecuteReader())
                //{
                //    while (reader.Read())
                //    {
                //        Console.WriteLine(String.Format("{0}, {1}",
                //            reader[0], reader[1]));
                //    }
                //}
            }
        }
    }
}
