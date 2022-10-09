using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;

namespace ConsoleApp_EasyLogger
{
    public class Logger
    {
        private bool _IsEnabled = false;
        public bool IsEnabled
        {
            get => _IsEnabled;
            set
            {
                if (value && !_IsEnabled)
                {
                    NeedFirstLine = true;
                }
                _IsEnabled = value;
            }
        }
        public bool NeedFirstLine = true;

        private string ActiveLogPath;
        private readonly List<string> LogList = new List<string>();
        private List<string> DeepCopyList = new List<string>();
        private volatile bool Busy = false;
        private readonly object LockLoggingPath = new object();
        private readonly object LockLoggingList = new object();
        private readonly Timer LogTimer;
        private readonly int MaxFileSize;
        private readonly int MaxNumberOfFile;
        private readonly string FirstMessage;
        private readonly bool IsSubjectToPowerLoss;
        public Logger(string logPath, string folderName, string fileName, int maxFileSize, int maxNumOfFiles, bool enableNow, string header = null, bool isSubjectToPowerLoss = false)
        {
            IsEnabled = enableNow;
            MaxFileSize = maxFileSize;
            MaxNumberOfFile = maxNumOfFiles;
            IsSubjectToPowerLoss = isSubjectToPowerLoss;
            ChangeFileLocation(Path.Combine(logPath, folderName, fileName));

            LogTimer = new Timer(5);
            LogTimer.Elapsed += TimerElasped;

            FirstMessage = header ?? $"Beginning of Logging: OS Version={Environment.OSVersion} | {DateTime.Now:G}";
        }

        public void ChangeFileLocation(string newLogPath)
        {
            lock (LockLoggingPath)
            {
                ActiveLogPath = newLogPath;
            }
        }

        public void Log(string message)
        {
            lock (LockLoggingList)
            {
                LogList.Add(message);
            }
            if (!Busy)
            {
                Busy = true;
                LogTimer.Enabled = true;
            }
        }
        public void Log(string message, Exception ex)
        {
            Log($"{message} Exception => {ex}");
        }

        private void TimerElasped(object source, ElapsedEventArgs e)
        {
            LogTimer.Enabled = false;
            if (IsEnabled)
            {
                Busy = true;
                LogMessage();
            }
            else
            {
                Busy = false;
            }
        }

        private void LogMessage()
        {
            bool KeepLogging = true;
            string path;
            lock (LockLoggingPath)
            {
                path = ActiveLogPath;
            }

            DeepCopyList = GetList();
            if (DeepCopyList.Count > 0)
            {
                KeepLogging = LogToFile(path);
            }
            else
            {
                // Since there is nothing to log, check to see if file is not to big.
                // if getting the exact file size is more important than resources.
                // put this at top
                FileInfo file = new FileInfo(path);
                if (file.Exists && file.Length >= MaxFileSize)
                {
                    KeepLogging = ArchiveFile(file);
                }
                else
                {
                    // Noting to log and file size is good, make sure not to many files.
                    KeepLogging = CheckMaxFilesNumber(file);
                }
            }

            // If there is no work to do shut down logger until next message comes in.
            LogTimer.Enabled = KeepLogging;
            // This volatile and atomic bool syncs the two threads with no race condition.
            Busy = KeepLogging;
        }

        private List<string> GetList()
        {
            List<string> result;
            lock (LockLoggingList)
            {
                result = new List<string>(LogList);
                LogList.Clear();
            }
            return result;
        }

        private bool LogToFile(string path)
        {
            bool keepLogging = true;
            try
            {
                if (NeedFirstLine)
                {
                    NeedFirstLine = false;
                    DeepCopyList.Insert(0, FirstMessage);
                }
                string output = $"{string.Join(Environment.NewLine, DeepCopyList)}{Environment.NewLine}";
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(new FileInfo(path).DirectoryName);
                }
                if (IsSubjectToPowerLoss)
                {
                    using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write))
                    {
                        byte[] byteData = Encoding.UTF8.GetBytes(output);
                        fs.Write(byteData, 0, byteData.Length);
                        fs.Flush(true);
                    }
                }
                else
                {
                    File.AppendAllText(path, output);
                }
            }
            catch { keepLogging = false; }

            return keepLogging;
        }

        private bool ArchiveFile(FileInfo file)
        {
            bool result;
            string newPath = $"Archived_{DateTime.Now:yyyy-MM-dd-T-HH-mm-ss}_{file.Name}";
            newPath = Path.Combine(file.DirectoryName, newPath);
            try
            {
                file.MoveTo(newPath);
                NeedFirstLine = true;
                result = true;
            }
            catch { result = false; }

            return result;
        }

        private bool CheckMaxFilesNumber(FileInfo file)
        {
            bool result;
            try
            {
                string[] files = Directory.GetFiles(file.Directory.FullName, "*", SearchOption.TopDirectoryOnly);
                if (files.Length > MaxNumberOfFile)
                {
                    int indexOfOldest = 0;
                    DateTime proposedTime = DateTime.Now;
                    for (int i = 0; i < files.Length; i++)
                    {
                        FileInfo testfile = new FileInfo(files[i]);
                        if (testfile.Exists)
                        {
                            DateTime fileTime = testfile.CreationTime;
                            if (fileTime < proposedTime)
                            {
                                proposedTime = fileTime;
                                indexOfOldest = i;
                            }
                        }
                    }
                    File.Delete(files[indexOfOldest]);
                    result = true;
                }
                else
                {
                    result = false;
                }
            }
            catch { result = false; }

            return result;
        }
    }
}
