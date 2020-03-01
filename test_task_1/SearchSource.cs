using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using static test_task_1.Program;

namespace test_task_1
{
    public class SearchSource
    {
        public const string RESULTS_TABLE_NAME = "Results_test";
        public const string ERRORS_TABLE_NAME = "Errors_test";
        //указанный пользователем каталог
        private string catalog_path;
        //ссылка на рабочую очередь файлов
        private Queue<string> files_to_handle;
        //ссылка на список ошибок
        private List<ErrorResult> errors;
        //переменная, сообщающая о конце работы SearchSource
        public bool Works_done { get; private set; }

        //locker`ы
        object locker_queue;
        object locker_errors;
        public SearchSource(string catalog_path, Queue<string> files_to_handle, List<ErrorResult> errors)
        {
            this.files_to_handle = files_to_handle;
            this.catalog_path = catalog_path;
            this.errors = errors;
            this.locker_queue = Stuff.locker_queue;
            this.locker_errors = Stuff.locker_errors;
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
            catch (Exception e)
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
}
