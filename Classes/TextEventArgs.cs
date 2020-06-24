using System;

namespace ASTAWebServer
{
    /// <summary>
    /// using in other class 
    /// public delegate void InfoMessage(object sender, TextEventArgs e); 
    /// public event InfoMessage EvntInfoMessage; 
    /// EvntInfoMessage?.Invoke(this, new TextEventArgs("info message to target class")); 
    /// using in the caller class: 
    /// reader.EvntInfoMessage += Write_text; 
    /// signature of method: 
    /// void Write_text(object sender, TextEventArgs e){ sender as (className); e.Action; } 
    /// </summary>
    public class TextEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public TextEventArgs(string message)
        {
            Message = message;
        }
    }
}