using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace test_task_1
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

            string table_name = Stuff.RESULTS_TABLE_NAME;
            return "INSERT INTO " + table_name + " (HashSum,FileName,FilePath)\n" +
                "VALUES (" + "\'" + hash_sum + "\'" + "," + "\'" + file_name + "\'" + "," + "\'" + file_path + "\'" + ");";
        }
    }
}
