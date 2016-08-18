using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Net.WebSockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Hubot_MSGroupChatAdapterService
{
    public partial class Hubot_MSGroupChatAdapterService : ServiceBase
    {
        private readonly string _hubotUri;
        private readonly Uri _userSipUri;
        private readonly CancellationToken _cancellationToken = new CancellationTokenSource().Token;
        private ClientWebSocket _clientWebSocket;
        private readonly EventLog _eventLog;
        private readonly GroupChat _groupChat;

        public Hubot_MSGroupChatAdapterService(string hubotUri)
        {
            if (string.IsNullOrEmpty(hubotUri))
                throw new ArgumentException("Value cannot be null or empty.", nameof(hubotUri));
            _hubotUri = hubotUri;

            InitializeComponent();

            _eventLog = new EventLog("Hubot_MSGroupChatAdapterServiceLog");
            ((ISupportInitialize)(_eventLog)).BeginInit();
            _eventLog.Source = "Hubot_MSGroupChatAdapterService";
            _eventLog.Log = "";  // automatch to event source
            if (!EventLog.SourceExists("Hubot_MSGroupChatAdapterService"))
            {
                EventLog.CreateEventSource("Hubot_MSGroupChatAdapterService", "Application");
            }
            ((ISupportInitialize)(_eventLog)).EndInit();
            

            _userSipUri = new Uri(ConfigurationManager.AppSettings["UserSipUri"]);
            var ocsServer = ConfigurationManager.AppSettings["OcsServer"];
            var ocsUsername = ConfigurationManager.AppSettings["OcsUsername"];
            // If we can't find the password in the registry, set to "default" and fail later.
            var password = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\hubot-msgc", "password", "default");
            var ocsPassword = password?.ToString() ?? "default";
            if (ocsPassword.Equals("default"))
            {
                _eventLog.WriteEntry(@"Failed to find password key in HKEY_LOCAL_MACHINE\SOFTWARE\hubot-msg");
            }
            ocsPassword = @"p@ssw0rd";
            var lookupServerUri = new Uri(ConfigurationManager.AppSettings["LookupServerUri"]);
            var chatRoomName = ConfigurationManager.AppSettings["ChatRoomName"];

            _groupChat = new GroupChat(_eventLog, _userSipUri, ocsServer, ocsUsername, ocsPassword, lookupServerUri, chatRoomName);
        }

        public void OnStartPublic(string[] args)
        {
            OnStart(args);
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            _eventLog.WriteEntry("OnStart");

            try
            {
                _clientWebSocket = ConnectToHubotAsync().Result;
                Task.Run(() => ListenAsync(), _cancellationToken);
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception while connecting to Hubot: " + exception.Message, EventLogEntryType.Error);
            }

            try
            {
                _groupChat.TextMessageReceived += GroupChatTextMessageReceivedAsync;
                _groupChat.Connect();
                Task.Run(() => _groupChat.SendAsync("Connected to GroupChat and Hubot."), _cancellationToken);
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception connecting to GroupChat: " + exception.Message, EventLogEntryType.Error);
            }
            _eventLog.WriteEntry("Connected to GroupChat and Hubot.");
        }

        protected override void OnStop()
        {
            base.OnStop();
            _eventLog.WriteEntry("OnStop");

            try
            {
                // Leave the room.
                // TODO send Hubot room leave message
                _groupChat.Disconnect();
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception disconnecting from GroupChat: " + exception.Message, EventLogEntryType.Error);
            }

            try
            {
                _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Adapter service stopping",
                    _cancellationToken).Wait(_cancellationToken);
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception disconnecting from Hubot: " + exception.Message, EventLogEntryType.Error);
            }
        }

        private async Task<ClientWebSocket> ConnectToHubotAsync()
        {
            var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(_hubotUri), _cancellationToken);
            return socket;
        }

        private async Task ListenAsync()
        {
            const int receiveChunkSize = 1024;
            try
            {
                while (_clientWebSocket.State == WebSocketState.Open)
                {
                    var buffer = new byte[receiveChunkSize];
                    WebSocketReceiveResult result;
                    var stringResult = new StringBuilder();
                    do
                    {
                        result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await
                                _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty,
                                    CancellationToken.None);
                        }
                        else
                        {
                            var str = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            stringResult.Append(str);
                        }
                    } while (!result.EndOfMessage);
                    var textMessage = JsonConvert.DeserializeObject<TextMessage>(stringResult.ToString());
                    HubotTextMessageReceivedAsync(this, new TextMessageReceivedEventArgs(textMessage));
                }
            }
            catch (Exception e)
            {
                _eventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);
            }
        }

        private async void HubotTextMessageReceivedAsync(object sender, TextMessageReceivedEventArgs e)
        {
            await _groupChat.SendAsync(e.TextMessage.Text);
        }

        private async void GroupChatTextMessageReceivedAsync(object sender, TextMessageReceivedEventArgs e)
        {
            // Don't send messages from Hubot back to itself...
            var userSipUri = new Uri(e.TextMessage.UserName);
            if (!userSipUri.Equals(_userSipUri))
            {
                await SendToHubotAsync(e.TextMessage);
            }
        }

        private async Task SendToHubotAsync(TextMessage textMessage)
        {
            var stringMessage = JsonConvert.SerializeObject(textMessage);
            _eventLog.WriteEntry("Sending:" + stringMessage);
            var sendBytes = Encoding.UTF8.GetBytes(stringMessage);
            var sendBuffer = new ArraySegment<byte>(sendBytes);
            try
            {
                await _clientWebSocket.SendAsync(
                    sendBuffer,
                    WebSocketMessageType.Text, true, _cancellationToken);
            }
            catch (Exception e)
            {
                _eventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);
            }
        }
    }
}
