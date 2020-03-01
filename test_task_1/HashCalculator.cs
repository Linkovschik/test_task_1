using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using static test_task_1.Program;
using static test_task_1.SearchSource;

namespace test_task_1
{
    public class HashCalculator
    {
        Queue<string> files_to_handle;
        List<ComplResult> results;
        SearchSource search_source;
        List<ErrorResult> errors;
        //ссылки на locker`ы
        object locker_queue;
        object locker_results;
        object locker_errors;
        public bool Works_done { get; private set; }
        public HashCalculator(Queue<string> queue, List<ComplResult> results, List<ErrorResult> errors, SearchSource search_source)
        {
            this.files_to_handle = queue;
            this.results = results;
            this.search_source = search_source;
            this.errors = errors;
            this.locker_queue = Stuff.locker_queue;
            this.locker_results = Stuff.locker_results;
            this.locker_errors = Stuff.locker_errors;
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
                        if (stream != null)
                        {
                            stream.Close();
                            stream.Dispose();
                        }
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
                    if (md5 != null) md5.Dispose();
                }

            }
        }
    }
}
