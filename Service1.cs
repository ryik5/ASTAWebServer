using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using System;
using System.Configuration.Install;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;


namespace ASTAWebServer
{
    public partial class ASTAWebServer : ServiceBase
    {
        static WebSocketServer aServer;
        /// <summary>
        /// Store the list of online users. Wish I had a ConcurrentList. 
        /// </summary>
        protected static System.Collections.Concurrent.ConcurrentDictionary<User, string> OnlineUsers;


        private System.Timers.Timer timer = null;
        private System.Threading.Thread webThread = null;
        static readonly Logger log = new Logger();

        public ASTAWebServer()
        {
            InitializeComponent();
        }


        protected override void OnStart(string[] args)
        {
            Start();
        }

        internal void Start()
        {
            OnlineUsers = new System.Collections.Concurrent.ConcurrentDictionary<User, string>();
            //https://github.com/Olivine-Labs/Alchemy-Websockets
            //https://docs.supersocket.net/v2-0/en-US/Get-the-connected-event-and-closed-event-of-a-connection
            aServer = new WebSocketServer(5000, System.Net.IPAddress.Any)
            {
                OnReceive = OnReceive,
                OnSend = OnSend,
                OnConnected = OnConnect,
                OnDisconnect = OnDisconnect,
                TimeOut = new TimeSpan(0, 5, 0)
            };

            if (webThread == null)
            {
                webThread = new System.Threading.Thread(new System.Threading.ThreadStart(StartWebSocket));
                webThread.SetApartmentState(System.Threading.ApartmentState.STA);
                webThread.IsBackground = true;
            }
            webThread.Start();

            timer = new System.Timers.Timer(10000);//создаём объект таймера
            timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
            timer.Enabled = true;
            timer.Start();
        }

        protected override void OnStop()
        {
            try
            {
                timer.Enabled = false;
                timer?.Stop();
                timer?.Dispose();
                WriteString("timer was stoped.");
            }
            catch (Exception err)
            {
                WriteString("timer wasn't stoped: " + err.Message);
            }
            try
            {
                aServer.Stop();
                aServer?.Dispose();
                OnlineUsers = null;
                WriteString("Websocket server was stoped.");
            }
            catch (Exception err)
            {
                WriteString("Websocket server wasn't stoped: " + err.Message);
            }

            try
            {
                webThread?.Abort();
                WriteString("Websocket's thread was stoped.");
            }
            catch (Exception err)
            {
                WriteString("Websocket's thread wasn't stoped: " + err.Message);
            }
        }

        public void WriteString(string text)
        {
            if (log != null)
                log.WriteString(text);
        }

        private void StartWebSocket()
        {
            // Initialize the server on port 5000, accept any IPs, and bind events.
            aServer.Start();
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            timer.Enabled = false;
            timer.Stop();

            WriteString($"Служба '{nameof(ASTAWebServer)}' активная...");

            timer.Enabled = true;
            timer.Start();
        }

        private void WebSocket_EvntInfoMessage(object sender, TextEventArgs e)
        {
            WriteString(e.Message);
        }

        /// <summary>
        /// Event fired when a client connects to the Alchemy Websockets server instance.
        /// Adds the client to the online users list.
        /// </summary>
        /// <param name="context">The user's connection context</param>
        public void OnConnect(UserContext context)
        {
            WriteString("Client Connected from: " + context.ClientAddress);

            var me = new User { Context = context };

            OnlineUsers.TryAdd(me, String.Empty);
        }

        /// <summary>
        /// Event fired when a data is received from the Alchemy Websockets server instance.
        /// Parses data as JSON and calls the appropriate message or sends an error message.
        /// </summary>
        /// <param name="context">The user's connection context</param>
        public void OnReceive(UserContext context)
        {
            var json = context.DataFrame.ToString();
            WriteString($"От: \"{context.ClientAddress}\" получены \"сырые\" данные: {json}");

            Response r = null;
            try
            {
                // <3 dynamics
                dynamic obj = JsonConvert.DeserializeObject(json);

                WriteString($"Десериализованные данные: {obj}");
                switch ((int)obj.Type)
                {
                    case (int)CommandType.Register:
                        r = new Response { Type = ResponseType.Message, Data = $"Вы отправили {obj?.Name}" };
                        WriteString($"Получен запрос на регистрацию: {obj?.Name?.Value}");
                        try { Register(obj.Name.Value, context); }
                        catch (Exception err) { WriteString($"Ошибка Register: {err.Message}"); }
                        break;

                    case (int)CommandType.Message:
                        r = new Response { Type = ResponseType.Message, Data = $"Вы отправили {obj?.Data}" };
                        WriteString($"Получено сообщение: {obj?.Data?.Value}");
                        try { ChatMessage(obj.Data.Value, context); }
                        catch (Exception err) { WriteString($"Ошибка ChatMessage: {err.Message}"); }
                        break;

                    case (int)CommandType.NameChange:
                        r = new Response { Type = ResponseType.Message, Data = $"Вы отправили {obj?.Name}" };
                        WriteString($"Смена имени: {obj?.Name?.Value}");
                        try { NameChange(obj.Name.Value, context); }
                        catch (Exception err) { WriteString($"Ошибка NameChange: {err.Message}"); }
                        break;
                }
            }
            catch (Exception e) // Bad JSON! For shame.
            {
                r = new Response { Type = ResponseType.Message, Data = $" Сейчас {DateTime.Now.ToString("yyyy-MM-dd hh:MM:ss")} и ты спросил {json}{Environment.NewLine} это ошибка: {e.Message}" };
            }
            context.Send(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Event fired when the Alchemy Websockets server instance sends data to a client.
        /// Logs the data to the console and performs no further action.
        /// </summary>
        /// <param name="context">The user's connection context</param>
        public void OnSend(UserContext context)
        {
            var json = context.DataFrame.ToString();
            WriteString($"Отправил: {context.ClientAddress} сообщение: {json}");
        }

        /// <summary>
        /// Event fired when a client disconnects from the Alchemy Websockets server instance.
        /// Removes the user from the online users list and broadcasts the disconnection message
        /// to all connected users.
        /// </summary>
        /// <param name="context">The user's connection context</param>
        public void OnDisconnect(UserContext context)
        {
            WriteString("Client Disconnected : " + context.ClientAddress);
            var user = OnlineUsers.Keys.Where(o => o.Context.ClientAddress == context.ClientAddress).Single();

            string trash; // Concurrent dictionaries make things weird

            OnlineUsers.TryRemove(user, out trash);

            if (!String.IsNullOrEmpty(user.Name))
            {
                var r = new Response { Type = ResponseType.Disconnect, Data = new { user.Name } };

                Broadcast(JsonConvert.SerializeObject(r));
            }

            BroadcastNameList();
        }

        /// <summary>
        /// Register a user's context for the first time with a username, and add it to the list of online users
        /// </summary>
        /// <param name="name">The name to register the user under</param>
        /// <param name="context">The user's connection context</param>
        private void Register(string name, UserContext context)
        {
            var u = OnlineUsers.Keys.Where(o => o.Context.ClientAddress == context.ClientAddress).Single();
            var r = new Response();

            if (ValidateName(name))
            {
                u.Name = name;

                r.Type = ResponseType.Connection;
                r.Data = new { u.Name };

                Broadcast(JsonConvert.SerializeObject(r));

                BroadcastNameList();
                OnlineUsers[u] = name;
            }
            else
            {
                SendError("Name is of incorrect length.", context);
            }
        }

        /// <summary>
        /// Broadcasts a chat message to all online usrs
        /// </summary>
        /// <param name="message">The chat message to be broadcasted</param>
        /// <param name="context">The user's connection context</param>
        private void ChatMessage(string message, UserContext context)
        {
            var u = OnlineUsers.Keys.Where(o => o.Context.ClientAddress == context.ClientAddress).Single();
            var r = new Response { Type = ResponseType.Message, Data = new { u.Name, Message = message } };

            Broadcast(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Update a user's name if they sent a name-change command from the client.
        /// </summary>
        /// <param name="name">The name to be changed to</param>
        /// <param name="aContext">The user's connection context</param>
        private void NameChange(string name, UserContext aContext)
        {
            var u = OnlineUsers.Keys.Where(o => o.Context.ClientAddress == aContext.ClientAddress).Single();

            if (ValidateName(name))
            {
                var r = new Response
                {
                    Type = ResponseType.NameChange,
                    Data = new { Message = u.Name + " is now known as " + name }
                };
                Broadcast(JsonConvert.SerializeObject(r));

                u.Name = name;
                OnlineUsers[u] = name;

                BroadcastNameList();
            }
            else
            {
                SendError("Name is of incorrect length.", aContext);
            }
        }

        /// <summary>
        /// Broadcasts an error message to the client who caused the error
        /// </summary>
        /// <param name="errorMessage">Details of the error</param>
        /// <param name="context">The user's connection context</param>
        private void SendError(string errorMessage, UserContext context)
        {
            var r = new Response { Type = ResponseType.Error, Data = new { Message = errorMessage } };

            context.Send(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Broadcasts a list of all online users to all online users
        /// </summary>
        private void BroadcastNameList()
        {
            var r = new Response
            {
                Type = ResponseType.UserCount,
                Data = new { Users = OnlineUsers.Values.Where(o => !String.IsNullOrEmpty(o)).ToArray() }
            };
            Broadcast(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Broadcasts a message to all users, or if users is populated, a select list of users
        /// </summary>
        /// <param name="message">Message to be broadcast</param>
        /// <param name="users">Optional list of users to broadcast to. If null, broadcasts to all. Defaults to null.</param>
        private void Broadcast(string message, System.Collections.Generic.ICollection<User> users = null)
        {
            if (users == null)
            {
                foreach (var u in OnlineUsers.Keys)
                {
                    u.Context.Send(message);
                }
            }
            else
            {
                foreach (var u in OnlineUsers.Keys.Where(users.Contains))
                {
                    u.Context.Send(message);
                }
            }
        }

        /// <summary>
        /// Checks validity of a user's name
        /// </summary>
        /// <param name="name">Name to check</param>
        /// <returns></returns>
        private bool ValidateName(string name)
        {
            var isValid = false;
            if (name.Length > 3 && name.Length < 25)
            {
                isValid = true;
            }

            return isValid;
        }

        /// <summary>
        /// Defines the type of response to send back to the client for parsing logic
        /// </summary>
        public enum ResponseType
        {
            Connection = 0,
            Disconnect = 1,
            Message = 2,
            NameChange = 3,
            UserCount = 4,
            Error = 255
        }

        /// <summary>
        /// Defines the response object to send back to the client
        /// </summary>
        public class Response
        {
            public ResponseType Type { get; set; }
            public dynamic Data { get; set; }
        }

        /// <summary>
        /// Holds the name and context instance for an online user
        /// </summary>
        public class User
        {
            public string Name = String.Empty;
            public UserContext Context { get; set; }
        }

        /// <summary>
        /// Defines a type of command that the client sends to the server
        /// </summary>
        public enum CommandType
        {
            Register = 0,
            NameChange,
            Message
        }
    }


    public class Logger
    {
        readonly object obj = new object();

        public Logger() { }

        public void WriteString(string text)
        {
            RecordEntry("Message", text);
        }
        private void RecordEntry(string eventText, string text)
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
    }

    /// <summary>
    /// Утилита саморегистрации
    /// </summary>
    [System.ComponentModel.RunInstaller(true)]
    public partial class ServiceInstallerUtility : Installer
    {
        //https://www.c-sharpcorner.com/article/installing-a-service-programmatically/
        //https://www.csharp-examples.net/install-net-service/
        //https://stackoverflow.com/questions/12201365/programmatically-remove-a-service-using-c-sharp

        //  private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        static ServiceInstaller serviceInstaller;
        readonly ServiceProcessInstaller processInstaller;

        public static readonly string serviceExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        public static readonly string serviceName = "ASTAWebServer";
        public static readonly string serviceDisplayName = "ASTA Web Server";
        public static readonly string serviceDescription = "ASTA Websocker SuperServer";
        private static int timeoutMilliseconds = 2000;
        public ServiceInstallerUtility()
        {
            //InitializeComponent();
            processInstaller = new ServiceProcessInstaller();
            processInstaller.Account = ServiceAccount.LocalSystem;

            serviceInstaller = new ServiceInstaller();
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = serviceName;
            serviceInstaller.AfterInstall += new InstallEventHandler(ServiceInstaller_AfterInstall);
            //           serviceInstaller.DelayedAutoStart = true;
            serviceInstaller.DisplayName = serviceDisplayName;
            serviceInstaller.Description = serviceDescription;

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }

        private void ServiceInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            using (ServiceController sc = new ServiceController(serviceInstaller.ServiceName))
            {
                sc.Start();
            }
        }

        public static bool Install()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new[] { "/i", serviceExePath });
            }
            catch { return false; }
            return true;
        }

        public static bool Uninstall()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new[] { "/u", serviceExePath });
            }
            catch { return false; }
            return true;
        }

        public static bool StopService()
        {
            ServiceController service = new ServiceController(serviceName);
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class WindowsServiceClass
    {
        #region SERVICE_ACCESS
        [Flags]
        public enum SERVICE_ACCESS : uint
        {
            STANDARD_RIGHTS_REQUIRED = 0xF0000,
            SERVICE_QUERY_CONFIG = 0x00001,
            SERVICE_CHANGE_CONFIG = 0x00002,
            SERVICE_QUERY_STATUS = 0x00004,
            SERVICE_ENUMERATE_DEPENDENTS = 0x00008,
            SERVICE_START = 0x00010,
            SERVICE_STOP = 0x00020,
            SERVICE_PAUSE_CONTINUE = 0x00040,
            SERVICE_INTERROGATE = 0x00080,
            SERVICE_USER_DEFINED_CONTROL = 0x00100,
            SERVICE_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED |
                SERVICE_QUERY_CONFIG |
                SERVICE_CHANGE_CONFIG |
                SERVICE_QUERY_STATUS |
                SERVICE_ENUMERATE_DEPENDENTS |
                SERVICE_START |
                SERVICE_STOP |
                SERVICE_PAUSE_CONTINUE |
                SERVICE_INTERROGATE |
                SERVICE_USER_DEFINED_CONTROL)
        }
        #endregion
        #region SCM_ACCESS
        [Flags]
        public enum SCM_ACCESS : uint
        {
            STANDARD_RIGHTS_REQUIRED = 0xF0000,
            SC_MANAGER_CONNECT = 0x00001,
            SC_MANAGER_CREATE_SERVICE = 0x00002,
            SC_MANAGER_ENUMERATE_SERVICE = 0x00004,
            SC_MANAGER_LOCK = 0x00008,
            SC_MANAGER_QUERY_LOCK_STATUS = 0x00010,
            SC_MANAGER_MODIFY_BOOT_CONFIG = 0x00020,
            SC_MANAGER_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED |
                SC_MANAGER_CONNECT |
                SC_MANAGER_CREATE_SERVICE |
                SC_MANAGER_ENUMERATE_SERVICE |
                SC_MANAGER_LOCK |
                SC_MANAGER_QUERY_LOCK_STATUS |
                SC_MANAGER_MODIFY_BOOT_CONFIG
        }
        #endregion

        #region DeleteService
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteService(IntPtr hService);
        #endregion
        #region OpenService
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, SERVICE_ACCESS dwDesiredAccess);
        #endregion
        #region OpenSCManager
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr OpenSCManager(string machineName, string databaseName, SCM_ACCESS dwDesiredAccess);
        #endregion
        #region CloseServiceHandle
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseServiceHandle(IntPtr hSCObject);
        #endregion

        public delegate void InfoMessage(object sender, TextEventArgs e);
        public event InfoMessage EvntInfoMessage;

        public void Uninstall(string serviceName)
        {
            try
            {
                IntPtr schSCManager = OpenSCManager(null, null, SCM_ACCESS.SC_MANAGER_ALL_ACCESS);
                if (schSCManager != IntPtr.Zero)
                {
                    IntPtr schService = OpenService(schSCManager, serviceName, SERVICE_ACCESS.SERVICE_ALL_ACCESS);
                    if (schService != IntPtr.Zero)
                    {
                        if (DeleteService(schService) == false)
                        {
                            EvntInfoMessage?.Invoke(this, new TextEventArgs($"DeleteService failed {Marshal.GetLastWin32Error()}"));

                            //System.Windows.Forms.MessageBox.Show(
                            //    string.Format("DeleteService failed {0}", Marshal.GetLastWin32Error()));
                        }
                    }
                    CloseServiceHandle(schSCManager);
                    // if you don't close this handle, Services control panel
                    // shows the service as "disabled", and you'll get 1072 errors
                    // trying to reuse this service's name
                    CloseServiceHandle(schService);

                }
            }
            catch (System.Exception ex)
            {
                EvntInfoMessage?.Invoke(this, new TextEventArgs(ex.Message));
                //System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }
    }


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