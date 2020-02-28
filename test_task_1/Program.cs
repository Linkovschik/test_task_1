using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace test_task_1
{
    class Program
    {
        public class ComplResult
        {
            private string hash_sum;
            private string file_name;
            private string file_path;
            public ComplResult(string hash_sum, string path)
            {
                this.hash_sum = hash_sum;
                this.file_path = path;
                this.file_name = Path.GetFileName(file_path);
            }
            public string GetAddQuery()
            {
                string table_name = "Results";
                return "INSERT INTO " +  table_name + " (HashSum,FileName,FilePath)\n" +
                    "VALUES (" + "\'" + hash_sum + "\'" + "," + "\'" + file_name + "\'" + "," + "\'" + file_path + "\'" + ");"; 
            }
        }
        public class ErrorResult
        {
            private string message;
            private string file_name;
            private string file_path;
            public ErrorResult(string message, string path)
            {
                this.message = message;
                this.file_path = path;
                this.file_name = Path.GetFileName(file_path);
            }
            public string GetAddQuery()
            {
                string table_name = "Errors";
                return "INSERT INTO " + table_name + " (Message,FileName,FilePath)\n" +
                    "VALUES (" + "\'" + message + "\'" + "," + "\'" + file_name + "\'" + "," + "\'" + file_path + "\'" + ");";
            }
        }

        static object locker_queue = new object();
        static object locker_results = new object();
        static object locker_errors = new object();
        static object locker_db = new object();

        static void Main(string[] args)
        {

            Console.WriteLine("На следующей строчке введите путь к каталогу:");
            string catalog_path = Console.ReadLine();

           

            //string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\Super\source\repos\test_task_1\test_task_1\Database1.mdf;Integrated Security=True";

            //using (SqlConnection connection = new SqlConnection(connectionString))
            //{
            //    connection.Open();
            //    Tuple<SqlCommand, SqlCommand> init_commands = new Tuple<SqlCommand, SqlCommand>(new SqlCommand(errors_creation_query, connection), new SqlCommand(results_creation_query, connection));
            //    init_commands.Item1.ExecuteReader().Close();
            //    init_commands.Item2.ExecuteReader().Close();
            //    connection.Close();
            //    connection.Dispose();
            //}

            Queue<string> file_queue;
            List<ComplResult> calculate_results;
            List<ErrorResult> errors;
            file_queue =new Queue<string>();
            calculate_results = new List<ComplResult>();
            errors = new List<ErrorResult>();
            SearchSource search_source = new SearchSource(catalog_path, file_queue, errors);
            HashCalculator hash_calculator = new HashCalculator(file_queue, calculate_results, errors, search_source);
            DbSaver dbSaver = new DbSaver(calculate_results, errors, hash_calculator, @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\Super\source\repos\test_task_1\test_task_1\Database1.mdf;Integrated Security=True");
            

            Thread read_thread = new Thread(new ThreadStart(search_source.StartSearch));
            read_thread.Name = "Read";
            read_thread.Start();

            Thread work_thread = new Thread(new ThreadStart(hash_calculator.CalculateMD5));
            work_thread.Name = "Work";
            work_thread.Start();

            Thread save_thread = new Thread(new ThreadStart(dbSaver.SaveToDB));
            save_thread.Name = "SaveToDb";
            save_thread.Start();

            while (true)
            {
                if ((read_thread.ThreadState == ThreadState.Stopped && work_thread.ThreadState == ThreadState.Stopped && save_thread.ThreadState == ThreadState.Stopped))
                {
                    foreach (var res in calculate_results)
                    {
                        Console.WriteLine(res.GetAddQuery());
                    }
                    Console.WriteLine("Конец");

                    break;
                }
            }
            Console.ReadKey();


        }
        public class SearchSource
        {
            //указанный пользователем каталог
            private string catalog_path;
            //ссылка на рабочую очередь файлов
            private Queue<string> files_to_handle;
            //ссылка на список ошибок
            private List<ErrorResult> errors;
            //переменная, сообщающая о конце работы SearchSource
            public bool Works_done { get; private set; }
            public SearchSource(string catalog_path, Queue<string> files_to_handle, List<ErrorResult> errors)
            {
                this.files_to_handle = files_to_handle;
                this.catalog_path = catalog_path;
                this.errors = errors;
                Works_done = false;
            }
            public void StartSearch()
            {
                search(catalog_path, files_to_handle, errors);
                Works_done = true;
            }
            private void search(string catalog_path, Queue<string> files_to_handle, List<ErrorResult> errors)
            {
                try
                {
                    string[] files = Directory.GetFiles(catalog_path);
                    string[] directories = Directory.GetDirectories(catalog_path);
                    foreach (string file in files)
                    {
                        bool acquired_lock = false;
                        try
                        {
                            Monitor.Enter(locker_queue, ref acquired_lock);
                            files_to_handle.Enqueue(file);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Ошибка при обходе каталогов: " + e.Message);
                        }
                        finally
                        {
                            if (acquired_lock) Monitor.Exit(locker_queue);
                        }
                    }
                    foreach (string direcory in directories)
                    {
                        search(direcory, files_to_handle, errors);
                    }
                }
                catch(Exception e)
                {
                    bool acquired_lock = false;
                    try
                    {
                        Monitor.Enter(locker_errors, ref acquired_lock);
                        errors.Add(new ErrorResult(e.Message, catalog_path));
                    }
                    finally
                    {
                        if (acquired_lock) Monitor.Exit(locker_queue);
                    }
                }
                
            }
           
            


        }
        public class HashCalculator
        {
            Queue<string> files_to_handle;
            List<ComplResult> results;
            SearchSource search_source;
            List<ErrorResult> errors;
            public bool Works_done { get; private set; }
            public HashCalculator(Queue<string> queue, List<ComplResult> results, List<ErrorResult> errors, SearchSource search_source)
            {
                this.files_to_handle = queue;
                this.results = results;
                this.search_source = search_source;
                this.errors = errors;
                this.Works_done = false;
            }
            public void CalculateMD5()
            {
                while (!search_source.Works_done || files_to_handle.Count > 0)
                {
                    if (files_to_handle.Count <= 0)
                        continue;
                    calculteNextMD5();
                }
                Works_done = true;

            }
            private void calculteNextMD5()
            {
                bool acquired_locker_queue = false;
                string filename = null;
                try
                {
                    Monitor.Enter(locker_queue, ref acquired_locker_queue);
                    if (acquired_locker_queue && files_to_handle.Count > 0)
                        filename = files_to_handle.Dequeue();
                }
                finally
                {
                    if (acquired_locker_queue) Monitor.Exit(locker_queue);
                }
                if (filename != null)
                {
                    //пример расчёта взят с https://stackoverflow.com/questions/10520048/calculate-md5-checksum-for-a-file
                    using (var md5 = MD5.Create())
                    {
                        using (var stream = File.OpenRead(filename))
                        {
                            var hash = md5.ComputeHash(stream);
                            string hash_sum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            bool acquired_locker_results = false;
                            try
                            {
                                Monitor.Enter(locker_results, ref acquired_locker_results);
                                if (acquired_locker_results)
                                {
                                    results.Add(new ComplResult(hash_sum, filename));
                                }
                                    
                            }
                            finally
                            {
                                if (acquired_locker_results) Monitor.Exit(locker_results);
                            }
                        }
                    }

                }
            }
        }
        public class DbSaver
        {
            private List<ComplResult> results;
            private List<ErrorResult> errors;
            private HashCalculator hash_calculator;
            private string connection_string;
            public DbSaver(List<ComplResult> results, List<ErrorResult> errors, HashCalculator hash_calculator, string connection_string)
            {
                this.results = results;
                this.errors = errors;
                this.hash_calculator = hash_calculator;
                this.connection_string = connection_string;
            }
            public void SaveToDB()
            {
                using (SqlConnection connection = new SqlConnection(connection_string))
                {
                    string errors_creation_query =
                    "if not exists(select * from sysobjects where name = 'Errors')" +
                    "CREATE TABLE [dbo].[Errors]" +
                    "([Id] INT IDENTITY(1,1)," +
                    "[Message] NVARCHAR(MAX) NOT NULL," +
                    "[FileName] NVARCHAR(MAX) NOT NULL," +
                    "[FilePath] NVARCHAR(MAX) NOT NULL," +
                    "PRIMARY KEY CLUSTERED([Id] ASC));";

                    string results_creation_query =
                    "if not exists(select * from sys.tables where name = 'Results')" +
                    "CREATE TABLE [dbo].[Results]" +
                    "([Id]  INT IDENTITY(1,1)," +
                    "[HashSum] NVARCHAR(MAX) NOT NULL," +
                    "[FileName] NVARCHAR(MAX) NOT NULL," +
                    "[FilePath] VARCHAR(MAX)  NOT NULL," +
                    "PRIMARY KEY CLUSTERED([Id] ASC));";

                    connection.Open();
                    SqlCommand results_table_creation = new SqlCommand(results_creation_query, connection);
                    results_table_creation.ExecuteNonQuery();
                    SqlCommand errors_table_creation = new SqlCommand(errors_creation_query, connection);
                    errors_table_creation.ExecuteNonQuery();
                    while (!hash_calculator.Works_done || results.Count > 0)
                    {
                        Console.WriteLine("ffffffffff");
                        if (results.Count <= 0)
                            continue;
                        saveToDB(connection);
                    }
                    connection.Close();
                    connection.Dispose();
                }
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
            }

        }
        
    }
}
