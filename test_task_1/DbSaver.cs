using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;

namespace test_task_1
{
    public class DbSaver
    {
        public const string RESULTS_TABLE_NAME = "Results_test";
        public const string ERRORS_TABLE_NAME = "Errors_test";
        private List<ComplResult> results;
        private List<ErrorResult> errors;
        private List<HashCalculator> hash_calculators;
        private string connection_string;
        //ссылки на locker`ы
        object locker_db;
        object locker_results;
        object locker_errors;
        public DbSaver(List<ComplResult> results, List<ErrorResult> errors, List<HashCalculator> hash_calculators, string connection_string)
        {
            this.results = results;
            this.errors = errors;
            this.hash_calculators = hash_calculators;
            this.connection_string = connection_string;
            this.locker_db = Stuff.locker_db;
            this.locker_results = Stuff.locker_results;
            this.locker_errors = Stuff.locker_errors;
        }
        public void SaveToDB()
        {
       
            SqlConnection connection = new SqlConnection(connection_string);
            //string drop_old_data_query =
            //"if EXISTS (select * from sysobjects where name = 'Results')" +
            //"DELETE FROM [dbo].[Results];" +
            //"if EXISTS (select * from sysobjects where name = 'Errors')" +
            //"DELETE FROM [dbo].[Errors];"; 
            string errors_creation_query =
            "if not exists(select * from sysobjects where name = '" + ERRORS_TABLE_NAME + "')" +
            "CREATE TABLE [dbo].[" + ERRORS_TABLE_NAME + "]" +
            "([Id] INT IDENTITY(1,1)," +
            "[Message] NVARCHAR(MAX) NOT NULL," +
            "[FileName] NVARCHAR(MAX) NOT NULL," +
            "[FilePath] NVARCHAR(MAX) NOT NULL," +
            "PRIMARY KEY CLUSTERED([Id] ASC));";

            string results_creation_query =
            "if not exists(select * from sysobjects where name = '" + RESULTS_TABLE_NAME + "')" +
            "CREATE TABLE [dbo].[" + RESULTS_TABLE_NAME + "]" +
            "([Id]  INT IDENTITY(1,1)," +
            "[HashSum] NVARCHAR(MAX) NOT NULL," +
            "[FileName] NVARCHAR(MAX) NOT NULL," +
            "[FilePath] VARCHAR(MAX)  NOT NULL," +
            "PRIMARY KEY CLUSTERED([Id] ASC));";

            connection.Open();
            //SqlCommand drop_tables = new SqlCommand(drop_old_data_query, connection);
            //drop_tables.ExecuteNonQuery();
            SqlCommand results_table_creation = new SqlCommand(results_creation_query, connection);
            results_table_creation.ExecuteNonQuery();
            SqlCommand errors_table_creation = new SqlCommand(errors_creation_query, connection);
            errors_table_creation.ExecuteNonQuery();
            while (!isHashCalculatorsDone() || results.Count > 0 || errors.Count > 0)
            {
                //Console.WriteLine("ffffffffff");
                if (results.Count <= 0 && errors.Count <= 0)
                    continue;
                saveToDB(connection);
            }
            connection.Close();
            connection.Dispose();
            
           
        }
        private bool isHashCalculatorsDone()
        {
            bool result = false;
            foreach (var calc in hash_calculators)
            {
                result = result || calc.Works_done;
            }
            return result;
        }
        private void saveToDB(SqlConnection connection)
        {
            bool acquired_locker = false;
            ComplResult result_to_add = null;
            try
            {
                Monitor.Enter(locker_results, ref acquired_locker);
                if (acquired_locker && results.Count > 0)
                {
                    result_to_add = results[0];
                    Stuff.checked_files_count++;
                    results.RemoveAt(0);
                }

            }
            finally
            {
                if (acquired_locker) Monitor.Exit(locker_results);
            }
            if (result_to_add != null)
            {
                bool acquired_locker_db = false;
                try
                {
                    Monitor.Enter(locker_db, ref acquired_locker_db);
                    if (acquired_locker_db)
                    {
                        SqlCommand command = new SqlCommand(result_to_add.GetAddQuery(), connection);
                        command.ExecuteReader().Close();
                    }

                }
                finally
                {
                    if (acquired_locker_db) Monitor.Exit(locker_db);
                }
            }
            bool acquired_locker_errors = false;
            ErrorResult error_to_add = null;
            try
            {
                Monitor.Enter(locker_errors, ref acquired_locker_errors);
                if (acquired_locker_errors && errors.Count > 0)
                {
                    error_to_add = errors[0];
                    Stuff.errors_found_count++;
                    errors.RemoveAt(0);
                }

            }
            finally
            {
                if (acquired_locker_errors) Monitor.Exit(locker_errors);
            }
            if (error_to_add != null)
            {
                bool acquired_locker_db = false;
                try
                {
                    Monitor.Enter(locker_db, ref acquired_locker_db);
                    if (acquired_locker_db)
                    {
                        SqlCommand command = new SqlCommand(error_to_add.GetAddQuery(), connection);
                        command.ExecuteReader().Close();
                    }

                }
                finally
                {
                    if (acquired_locker_db) Monitor.Exit(locker_db);
                }
            }
        }

    }
}
