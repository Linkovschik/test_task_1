namespace test_task_1
{
    public static class Stuff
    {
        public const string RESULTS_TABLE_NAME = "Results_test";
        public const string ERRORS_TABLE_NAME = "Errors_test";
        public const int WORK_THREADS_COUNT = 4;
        public static object locker_queue = new object();
        public static object locker_results = new object();
        public static object locker_errors = new object();
        public static object locker_db = new object();
        public static int checked_files_count = 0;
        public static int errors_found_count = 0;
    }
}
