using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Net.WebSockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.GroupChat;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Hubot_MSGroupChatAdapterService
{
    public partial class Hubot_MSGroupChatAdapterService : ServiceBase
    {
        private readonly string _hubotUri;
        private readonly string _userSipUri;
        private readonly string _ocsServer;
        private readonly string _ocsUsername;
        private readonly string _ocsPassword;
        private readonly Uri _lookupServerUri;
        private readonly string _chatRoomName;
        private readonly string _botName;
        private readonly CancellationToken _cancellationToken = new CancellationTokenSource().Token;
        private ChatRoomSession _chatRoomSession;
        private GroupChatEndpoint _groupChatEndpoint;
        private UserEndpoint _userEndpoint;
        private ClientWebSocket _clientWebSocket;
        private readonly EventLog _eventLog;

        public Hubot_MSGroupChatAdapterService(string hubotUri)
        {
            if (string.IsNullOrEmpty(hubotUri))
                throw new ArgumentException("Value cannot be null or empty.", nameof(hubotUri));
            _hubotUri = hubotUri;

            InitializeComponent();

            _eventLog = new EventLog("Hubot_MSGroupChatAdapterService");
            ((ISupportInitialize)(_eventLog)).BeginInit();
            if (!EventLog.SourceExists("Hubot_MSGroupChatAdapterService"))
            {
                EventLog.CreateEventSource("Hubot_MSGroupChatAdapterService", "Hubot_MSGroupChatAdapterService");
            }
            _eventLog.Source = "Hubot_MSGroupChatAdapterService";
            _eventLog.Log = "";  // Automatically matches log to source
            ((ISupportInitialize)(_eventLog)).EndInit();

            _userSipUri = ConfigurationManager.AppSettings["UserSipUri"];
            _ocsServer = ConfigurationManager.AppSettings["OcsServer"];
            _ocsUsername = ConfigurationManager.AppSettings["OcsUsername"];
            // If we can't find the password in the registry, set to "default" and fail later.
            var password = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\hubot-msgc", "password", "default");
            _ocsPassword = password?.ToString() ?? "default";
            if (_ocsPassword.Equals("default"))
            {
                _eventLog.WriteEntry(@"Failed to find password key in HKEY_LOCAL_MACHINE\SOFTWARE\hubot-msg");
            }
            _lookupServerUri = new Uri(ConfigurationManager.AppSettings["LookupServerUri"]);
            _chatRoomName = ConfigurationManager.AppSettings["ChatRoomName"];
            _botName = ConfigurationManager.AppSettings["BotName"];

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
                _userEndpoint = GroupChat.ConnectOfficeCommunicationServer(_userSipUri,
                    _ocsServer, _ocsUsername, _ocsPassword);

                _groupChatEndpoint = GroupChat.ConnectGroupChatServer(_userEndpoint, _lookupServerUri);

                var roomSnapshot = GroupChat.RoomSearchExisting(_groupChatEndpoint, _chatRoomName);
                _chatRoomSession = GroupChat.RoomJoinExisting(_groupChatEndpoint, roomSnapshot);

                _chatRoomSession.ChatMessageReceived += SessionChatMessageReceivedAsync;

                _chatRoomSession.EndSendChatMessage(
                    _chatRoomSession.BeginSendChatMessage("Connected to GroupChat and Hubot.", null, null));
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
                _chatRoomSession.EndLeave(_chatRoomSession.BeginLeave(null, null));
                _chatRoomSession.ChatMessageReceived -= SessionChatMessageReceivedAsync;

                // Disconnect from Group Chat and from OCS
                GroupChat.DisconnectGroupChatServer(_groupChatEndpoint);
                GroupChat.DisconnectOfficeCommunicationServer(_userEndpoint);
            }
            catch (Exception exception)
            {
                _eventLog.WriteEntry("Exception disconnecting from GroupChat: " + exception.Message, EventLogEntryType.Error);
            }
        }

        private async Task<ClientWebSocket> ConnectToHubotAsync()
        {
            var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(_hubotUri), _cancellationToken);
            return socket;
        }

        private async void ListenAsync()
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
                    _eventLog.WriteEntry("Got: " + stringResult);
                    _eventLog.WriteEntry("Parsed: " + textMessage);
                    var formattedOutboundChatMessage = new FormattedOutboundChatMessage();
                    formattedOutboundChatMessage.AppendPlainText(textMessage.Text);
                    // TODO parse URLs etc
                    _chatRoomSession.EndSendChatMessage(
                        _chatRoomSession.BeginSendChatMessage(formattedOutboundChatMessage, null, null));
                }
            }
            catch (Exception e)
            {
                _eventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);
            }
            finally
            {
                _clientWebSocket.Dispose();
            }
        }

        private async void SessionChatMessageReceivedAsync(object sender, ChatMessageReceivedEventArgs e)
        {
            _eventLog.WriteEntry("\tChat message received");

            _eventLog.WriteEntry($"\t from:[{e.Message.MessageAuthor}]");
            _eventLog.WriteEntry($"\t room:[{e.Message.ChatRoomName}]");
            _eventLog.WriteEntry($"\t body:[{e.Message.MessageContent}]");
            _eventLog.WriteEntry($"\t parts:[{e.Message.FormattedMessageParts.Count}]");
            foreach (var part in e.Message.FormattedMessageParts)
            {
                EventLog.WriteEntry($"\t part:[{part.RawText}]");
            }
            await SendToHubotAsync(e.Message);
        }

        private async Task SendToHubotAsync(ChatMessage message)
        {
            var textMessage = new TextMessage("text", message.MessageId, message.MessageAuthor.ToString(),
                message.ChatRoomName, message.MessageContent);
            var stringMessage = JsonConvert.SerializeObject(textMessage);
            _eventLog.WriteEntry("Sending:" + stringMessage);
            var sendBytes = Encoding.UTF8.GetBytes(stringMessage);
            var sendBuffer = new ArraySegment<byte>(sendBytes);
            await _clientWebSocket.SendAsync(
                sendBuffer,
                WebSocketMessageType.Text, true, _cancellationToken);
        }
    }
}
