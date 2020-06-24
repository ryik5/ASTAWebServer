using System;

namespace ASTAWebServer
{
    internal  class Logger
    {
        readonly object obj = new object();

        public  void WriteString(string text, string eventText = "Message")
        {
            RecordEntry(eventText, text);
        }
        private  void RecordEntry(string eventText, string text)
        {
            try
            {
                string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string pathToLog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + ".log");
                lock (obj)
                {
                    using (System.IO.StreamWriter writer = new System.IO.StreamWriter(pathToLog, true))
                    {
                        writer.WriteLine($"{DateTime.Now.ToString("yyyy.MM.dd|hh:mm:ss")}|{eventText}|{text}");
                        writer.Flush();
                    }
                }
            }
            catch { }
        }
    }
}
