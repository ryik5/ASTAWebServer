using System;

namespace ASTAWebServer
{
    internal class Logger
    {
        readonly object obj = new object();

        public void WriteString(string text, string eventText = "Message")
        {
            RecordEntry(text, eventText);
        }
        private void RecordEntry(string text, string eventText)
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string pathToLogDir, pathToLog;
            try
            {
                pathToLogDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), $"logs");
                if (!System.IO.Directory.Exists(pathToLogDir))
                     System.IO.Directory.CreateDirectory(pathToLogDir); 

                pathToLog = System.IO.Path.Combine(pathToLogDir, $"{DateTime.Now.ToString("yyyy-MM-dd")}.log");
                lock (obj)
                {
                    using (System.IO.StreamWriter writer = new System.IO.StreamWriter(pathToLog, true))
                    {
                        writer.WriteLine($"{DateTime.Now.ToString("yyyy.MM.dd|hh:mm:ss")}|{eventText}|{text}");
                        writer.Flush();
                    }
                }
            }
            catch (Exception err)
            {
                pathToLog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + ".log");
                lock (obj)
                {
                    using (System.IO.StreamWriter writer = new System.IO.StreamWriter(pathToLog, true))
                    {
                        writer.WriteLine($"{DateTime.Now.ToString("yyyy.MM.dd|hh:mm:ss")}|{err.ToString()}");
                        writer.Flush();
                    }
                }
            }
        }
    }
}