using System;

namespace ASTAWebServer
{
    internal static class Logger
    {
        public static void WriteString(string text)
        {
            RecordEntry("Message", text);
        }
        private static void RecordEntry(string eventText, string text)
        {
            try
            {
                object obj = new object();
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
