using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace test_task_1
{
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

            string table_name = Stuff.ERRORS_TABLE_NAME;
            return "INSERT INTO " + table_name + " (Message,FileName,FilePath)\n" +
                "VALUES (" + "\'" + message + "\'" + "," + "\'" + file_name + "\'" + "," + "\'" + file_path + "\'" + ");";
        }
    }
    
}
