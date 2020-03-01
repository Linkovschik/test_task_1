using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Collections.Generic;
namespace test_task_1
{
    class Program
    { 
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
                    Console.WriteLine("Введите путь к каталогу:");
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
            for (int i=0; i< Stuff.WORK_THREADS_COUNT; ++i)
            {
                hashCalculators.Add(new HashCalculator(file_queue, calculate_results, errors, search_source));
            }
            DbSaver dbSaver = new DbSaver(calculate_results, errors, hashCalculators, connection_string);
            

            Thread read_thread = new Thread(new ThreadStart(search_source.StartSearch));
            read_thread.Name = "Read";
            read_thread.Start();

            Thread[] work_threads = new Thread[Stuff.WORK_THREADS_COUNT];
            for (int i = 0; i < Stuff.WORK_THREADS_COUNT; ++i)
            {
                work_threads[i] = (new Thread(new ThreadStart(hashCalculators[i].CalculateMD5)));
            }
            for (int i = 0; i < Stuff.WORK_THREADS_COUNT; ++i)
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
                    Console.WriteLine("Все результаты записаны в таблицах "+Stuff.RESULTS_TABLE_NAME+" и " + Stuff.ERRORS_TABLE_NAME);
                    Console.WriteLine("Файлов проверено: " + Stuff.checked_files_count);
                    Console.WriteLine("Найдено ошибок: " + Stuff.errors_found_count);
                    Console.WriteLine("Нажмите любую клавишу для завершения работы...");
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
        
        
        
    }
}
