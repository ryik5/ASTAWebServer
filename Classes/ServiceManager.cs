using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace ASTAWebServer
{
    public class ServiceManager : IServiceManageable
    {
        static WebSocketServer aServer;
        /// <summary>
        /// Store the list of online users. Wish I had a ConcurrentList. 
        /// </summary>
        protected static System.Collections.Concurrent.ConcurrentDictionary<User, string> OnlineUsers;

        private System.Timers.Timer timer = null;
        private System.Threading.Thread webThread = null;

        public void OnPause()
        {
            throw new NotImplementedException();
        }

        public void OnStop()
        {
            try
            {
                timer.Enabled = false;
                timer?.Stop();
                timer?.Dispose();
                AddInfo("timer was stoped.");
            }
            catch (Exception err)
            {
                AddInfo("timer wasn't stoped: " + err.Message);
            }
            try
            {
                aServer.Stop();
                aServer?.Dispose();
                OnlineUsers = null;
                AddInfo("Websocket server was stoped.");
            }
            catch (Exception err)
            {
                AddInfo("Websocket server wasn't stoped: " + err.Message);
            }

            try
            {
                webThread?.Abort();
                webThread = null;
                AddInfo("Websocket's thread was stoped.");
            }
            catch (Exception err)
            {
                AddInfo("Websocket's thread wasn't stoped: " + err.Message);
            }
        }

        public void OnStart()
        {
            OnlineUsers = new System.Collections.Concurrent.ConcurrentDictionary<User, string>();
            //https://github.com/Olivine-Labs/Alchemy-Websockets
            //https://docs.supersocket.net/v2-0/en-US/Get-the-connected-event-and-closed-event-of-a-connection
            // Initialize the server on port 5000, accept any IPs, and bind events.
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
                webThread.IsBackground = true;
                webThread.SetApartmentState(System.Threading.ApartmentState.STA);
            }
            webThread.Start();

            timer = new System.Timers.Timer(30000);//создаём объект таймера
            timer.Enabled = true;
            timer.Start();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
            AddInfo("timer is running...");
        }


        public void AddInfo(string text)
        {
            Logger.WriteString(text);
        }

        private void StartWebSocket()
        {
            aServer.Start();
            AddInfo("Websocket server is waiting...");
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            timer.Enabled = false;
            timer.Stop();

            if (OnlineUsers != null && OnlineUsers?.Keys?.Count > 0)
            {
                foreach (var u in OnlineUsers.Keys)
                {
                    try
                    {
                        var r = new Response { Type = ResponseType.Message, Data = $"SendCollectedData" };
                        u.Context.Send(JsonConvert.SerializeObject(r));
                    }
                    catch (Exception err)
                    {
                        AddInfo($"Error: {err.Message}");
                    };

                    try { AddInfo($"{u.Name}({u.Context.ClientAddress}) отправьте информацию"); }
                    catch (Exception err)
                    {
                        AddInfo($"Error: {err.Message}");
                    };
                }
            }
            else
            {
                AddInfo($"Служба '{nameof(ASTAWebServer)}' активная...");
                AddInfo($"Нет ни одного подключенного клиента.");
            }

            timer.Enabled = true;
            timer.Start();
        }

        /// <summary>
        /// Event fired when a client connects to the Alchemy Websockets server instance.
        /// Adds the client to the online users list.
        /// </summary>
        /// <param name="context">The user's connection context</param>
        public void OnConnect(UserContext context)
        {
            AddInfo("Client is connected from: " + context.ClientAddress);

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
            AddInfo($"От: \"{context.ClientAddress}\" получены \"сырые\" данные: {json}");

            Response r = null;
            dynamic obj = null;
            try
            {
                // <3 dynamics
                obj = JsonConvert.DeserializeObject(json);

                AddInfo($"Десериализованные данные: {obj}");
            }
            catch (Exception e) // Bad JSON! For shame.
            {
                r = new Response { Type = ResponseType.Message, Data = $" Сейчас {DateTime.Now.ToString("yyyy-MM-dd hh:MM:ss")} и ты спросил {json}{Environment.NewLine} это ошибка: {e.Message}" };
            }

            if (obj != null)
            {
                switch ((int)obj.Type)
                {
                    case (int)CommandType.Register:
                        r = new Response { Type = ResponseType.Message, Data = $"Вы отправили {obj?.Name}" };
                        AddInfo($"Получен запрос на регистрацию: {obj?.Name?.Value}");
                        try { Register(obj.Name, context); }
                        catch (Exception err) { AddInfo($"Ошибка Register: {err.Message}"); }
                        break;

                    case (int)CommandType.Message:
                        r = new Response { Type = ResponseType.Message, Data = $"Вы отправили {obj?.Data}" };
                        AddInfo($"Получено сообщение: {obj?.Data?.Value}");
                        try { ChatMessage(obj.Data.Value, context); }
                        catch (Exception err) { AddInfo($"Ошибка ChatMessage: {err.Message}"); }
                        break;

                    case (int)CommandType.NameChange:
                        r = new Response { Type = ResponseType.Message, Data = $"Вы отправили {obj?.Name}" };
                        AddInfo($"Смена имени: {obj?.Name?.Value}");
                        try { NameChange(obj.Name.Value, context); }
                        catch (Exception err) { AddInfo($"Ошибка NameChange: {err.Message}"); }
                        break;
                }
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
            AddInfo($"Отправил: {context.ClientAddress} сообщение: {json}");
        }

        /// <summary>
        /// Event fired when a client disconnects from the Alchemy Websockets server instance.
        /// Removes the user from the online users list and broadcasts the disconnection message
        /// to all connected users.
        /// </summary>
        /// <param name="context">The user's connection context</param>
        public void OnDisconnect(UserContext context)
        {
            AddInfo("Client Disconnected : " + context.ClientAddress);
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
    /// Defines the response object to send back to the client
    /// </summary>
    public class Response
    {
        public ResponseType Type { get; set; }
        public dynamic Data { get; set; }
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
        ReadyToWork = 254,
        Error = 255
    }

    /// <summary>
    /// Defines a type of command that the client sends to the server
    /// </summary>
    public enum CommandType
    {
        Register = 0,
        NameChange = 1,
        Message = 2,
        DoWork = 254,
        Nope = 255
    }
}