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
                hash_sum = hash_sum.Replace("'", "");
                file_name = file_name.Replace("'", "");
                file_path = file_path.Replace("'", "");

                string table_name = RESULTS_TABLE_NAME;
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
                message = message.Replace("'", "");
                file_name = file_name.Replace("'", "");
                file_path = file_path.Replace("'", "");

                string table_name = ERRORS_TABLE_NAME;
                return "INSERT INTO " + table_name + " (Message,FileName,FilePath)\n" +
                    "VALUES (" + "\'" + message + "\'" + "," + "\'" + file_name + "\'" + "," + "\'" + file_path + "\'" + ");";
            }
        }

        static object locker_queue = new object();
        static object locker_results = new object();
        static object locker_errors = new object();
        static object locker_db = new object();
        const string RESULTS_TABLE_NAME = "Results_test";
        const string ERRORS_TABLE_NAME = "Errors_test";
        const int WORK_THREADS_COUNT = 4;

        static void Main(string[] args)
        {
            bool succesful_connection = false;
            string connection_string = null;
            while (!succesful_connection)
            {
                Console.WriteLine("Введите строку подключения:");
                connection_string = @Console.ReadLine().Replace("\"", "");
                SqlConnection connection = null;
                try
                {
                    connection = new SqlConnection(connection_string);
                    connection.Open();
                    succesful_connection = true;
                }
                catch(Exception e)
                {
                    //Console.WriteLine(e);
                    Console.WriteLine("Неверная строка подключения");
                    succesful_connection = false;
                    connection_string = null;
                }
                finally
                {
                    if (connection != null)
                    {
                        connection.Close();
                        connection.Dispose();
                    }
                }
            }

            bool succesful_reading = false;
            string catalog_path = null;
            while (!succesful_reading)
            {
                string[] files = null;
                string[] directories = null;
                try
                {
                    Console.WriteLine("На следующей строчке введите путь к каталогу:");
                    catalog_path = Console.ReadLine().Replace("\"", "");
                    files = Directory.GetFiles(catalog_path);
                    directories = Directory.GetDirectories(catalog_path);
                    if(files!=null && directories!=null)
                    {
                        succesful_reading = true;
                    }
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e);
                    Console.WriteLine("Указан неверный путь к каталогу");
                    succesful_reading = false;
                    files = null;
                    directories = null;
                    catalog_path = null;
                }
               
            }
            

            Queue<string> file_queue;
            List<ComplResult> calculate_results;
            List<ErrorResult> errors;
            file_queue =new Queue<string>();
            calculate_results = new List<ComplResult>();
            errors = new List<ErrorResult>();
            SearchSource search_source = new SearchSource(catalog_path, file_queue, errors);
            List<HashCalculator> hashCalculators = new List<HashCalculator>();
            for (int i=0; i< WORK_THREADS_COUNT; ++i)
            {
                hashCalculators.Add(new HashCalculator(file_queue, calculate_results, errors, search_source));
            }
            DbSaver dbSaver = new DbSaver(calculate_results, errors, hashCalculators, connection_string);
            

            Thread read_thread = new Thread(new ThreadStart(search_source.StartSearch));
            read_thread.Name = "Read";
            read_thread.Start();

            Thread[] work_threads = new Thread[WORK_THREADS_COUNT];
            for (int i = 0; i < WORK_THREADS_COUNT; ++i)
            {
                work_threads[i] = (new Thread(new ThreadStart(hashCalculators[i].CalculateMD5)));
            }
            for (int i = 0; i < WORK_THREADS_COUNT; ++i)
            {
                work_threads[i].Start();
            }

            Thread save_thread = new Thread(new ThreadStart(dbSaver.SaveToDB));
            save_thread.Name = "SaveToDb";
            save_thread.Start();

            while (true)
            {
                if ((read_thread.ThreadState == ThreadState.Stopped && IsThreadsDone(work_threads) && save_thread.ThreadState == ThreadState.Stopped))
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
        public static bool IsThreadsDone(Thread[] threads)
        {
            bool result = false;
            foreach (var thread in threads)
            {
                result = result || thread.ThreadState == ThreadState.Stopped;
            }
            return result;
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
                        finally
                        {
                            if (acquired_lock) Monitor.Exit(locker_queue);
                        }
                    }
                    foreach (string directory in directories)
                    {
                        search(directory, files_to_handle, errors);
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
                        if (acquired_lock) Monitor.Exit(locker_errors);
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
                    MD5 md5 = null;
                    try
                    {
                        md5 = MD5.Create();
                        FileStream stream = null;
                        try
                        {
                            stream = File.OpenRead(filename);
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
                        catch (Exception e)
                        {
                            bool acquired_locker_errors = false;
                            try
                            {
                                Monitor.Enter(locker_errors, ref acquired_locker_errors);
                                if (acquired_locker_errors)
                                {
                                    errors.Add(new ErrorResult(e.Message, filename));
                                }
                            }
                            finally
                            {
                                if (acquired_locker_errors) Monitor.Exit(locker_errors);
                            }
                        }
                        finally
                        {
                            if (stream!=null)
                            {
                                stream.Close();
                                stream.Dispose();
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        bool acquired_locker_errors = false;
                        try
                        {
                            Monitor.Enter(locker_errors, ref acquired_locker_errors);
                            if (acquired_locker_errors)
                            {
                                errors.Add(new ErrorResult(e.Message, filename));
                            }
                        }
                        finally
                        {
                            if (acquired_locker_errors) Monitor.Exit(locker_errors);
                        }
                    }
                    finally
                    {
                        if (md5!=null) md5.Dispose();
                    }

                }
            }
        }
        public class DbSaver
        {
            private List<ComplResult> results;
            private List<ErrorResult> errors;
            private List<HashCalculator> hash_calculators;
            private string connection_string;
            public DbSaver(List<ComplResult> results, List<ErrorResult> errors, List<HashCalculator> hash_calculators, string connection_string)
            {
                this.results = results;
                this.errors = errors;
                this.hash_calculators = hash_calculators;
                this.connection_string = connection_string;
            }
            public void SaveToDB()
            {
                try
                {
                    SqlConnection connection = new SqlConnection(connection_string);
                    //string drop_old_data_query =
                    //"if EXISTS (select * from sysobjects where name = 'Results')" +
                    //"DELETE FROM [dbo].[Results];" +
                    //"if EXISTS (select * from sysobjects where name = 'Errors')" +
                    //"DELETE FROM [dbo].[Errors];"; 
                    string errors_creation_query =
                    "if not exists(select * from sysobjects where name = '"+ ERRORS_TABLE_NAME + "')" +
                    "CREATE TABLE [dbo].[" + ERRORS_TABLE_NAME + "]" +
                    "([Id] INT IDENTITY(1,1)," +
                    "[Message] NVARCHAR(MAX) NOT NULL," +
                    "[FileName] NVARCHAR(MAX) NOT NULL," +
                    "[FilePath] NVARCHAR(MAX) NOT NULL," +
                    "PRIMARY KEY CLUSTERED([Id] ASC));";

                    string results_creation_query =
                    "if not exists(select * from sysobjects where name = '" + RESULTS_TABLE_NAME + "')" +
                    "CREATE TABLE [dbo].["+ RESULTS_TABLE_NAME + "]" +
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
                        if (results.Count <= 0 && errors.Count <=0)
                            continue;
                        saveToDB(connection);
                    }
                    connection.Close();
                    connection.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Console.WriteLine("Перезапустите программу и введите правильную строку подключения");
                }
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
}
