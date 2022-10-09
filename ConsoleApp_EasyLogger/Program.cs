using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp_EasyLogger
{
    class Program
    {
        private static Logger logger;
        private static Logger recorder;
        static void Main(string[] args)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            logger = new Logger(path, "LogFolder", "LogFile.txt", 5000, 4, true);

            // If you are just logging to a txt file and want the standard header and
            // are not making CSV recordings, do not pass anything like example above.
            string CSVheader = "Interaction, Delay, Temperature, Date and Time";
            recorder = new Logger(path, "RecordingFolder", "Recording.csv", 5000, 4, true, CSVheader, true);

            _ = Task.Run(() =>
            {
                Random r = new Random(14);
                for (int i = 0; i < 1000; i++)
                {
                    int delay = r.Next(0, 7);
                    delay = (delay == 4) ? 1000 : delay;
                    Thread.Sleep(delay);
                    TestLog($"{DateTime.Now:G} | {i}-iteration standing in for log message.");
                }
            });

            _ = Task.Run(() =>
            {
                Random r = new Random(11);
                for (int i = 0; i < 1000; i++)
                {
                    int delay = r.Next(0, 7);
                    delay = (delay == 4) ? 1000 : delay;
                    Thread.Sleep(delay);
                    TestRecord($"{i}, {delay}, {r.Next(85, 99)}, {DateTime.Now:G}");
                }
            });

            Console.ReadKey();
        }


        public static void TestRecord(string message)
        {
            if (recorder != null)
            {
                recorder.Log(message);
            }
        }

        public static bool EnableRecording
        {
            get => (recorder != null) && recorder.IsEnabled;
            set
            {
                if (recorder != null)
                {
                    recorder.IsEnabled = value;
                }
            }
        }

        public static void TestLog(string message)
        {
            if (logger != null)
            {
                logger.Log(message);
            }
        }

    }
}
