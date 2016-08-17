using System;
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
    public partial class Service : ServiceBase
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
        private readonly EventLog _eventLog = new EventLog();

        public Service(string hubotUri)
        {
            if (string.IsNullOrEmpty(hubotUri))
                throw new ArgumentException("Value cannot be null or empty.", nameof(hubotUri));
            _hubotUri = hubotUri;
            _userSipUri = ConfigurationManager.AppSettings["UserSipUri"];
            _ocsServer = ConfigurationManager.AppSettings["OcsServer"];
            _ocsUsername = ConfigurationManager.AppSettings["OcsUsername"];
            _ocsPassword =
                Registry.GetValue(@"HKEY_CURRENT_USER\hubot-msgc", "password", "default").ToString();
            _lookupServerUri = new Uri(ConfigurationManager.AppSettings["LookupServerUri"]);
            _chatRoomName = ConfigurationManager.AppSettings["ChatRoomName"];
            _botName = ConfigurationManager.AppSettings["BotName"];

            _eventLog.Source = ServiceName;
            _eventLog.Log = "Application";

            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _clientWebSocket = ConnectToHubotAsync().Result;
            Task.Run(() => ListenAsync(), _cancellationToken);

            _userEndpoint = GroupChat.ConnectOfficeCommunicationServer(_userSipUri,
                _ocsServer, _ocsUsername, _ocsPassword);

            _groupChatEndpoint = GroupChat.ConnectGroupChatServer(_userEndpoint, _lookupServerUri);

            var roomSnapshot = GroupChat.RoomSearchExisting(_groupChatEndpoint, _chatRoomName);
            _chatRoomSession = GroupChat.RoomJoinExisting(_groupChatEndpoint, roomSnapshot);

            _chatRoomSession.ChatMessageReceived += SessionChatMessageReceivedAsync;

            _chatRoomSession.EndSendChatMessage(
                _chatRoomSession.BeginSendChatMessage("Connected to GroupChat and Hubot...", null, null));
        }

        protected override void OnStop()
        {
            // Leave the room.
            // TODO send Hubot room leave message
            _chatRoomSession.EndLeave(_chatRoomSession.BeginLeave(null, null));
            _chatRoomSession.ChatMessageReceived -= SessionChatMessageReceivedAsync;

            // Disconnect from Group Chat and from OCS
            GroupChat.DisconnectGroupChatServer(_groupChatEndpoint);
            GroupChat.DisconnectOfficeCommunicationServer(_userEndpoint);
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
                    Console.Out.WriteLine("Got: " + stringResult);
                    Console.Out.WriteLine("Parsed: " + textMessage);
                    var formattedOutboundChatMessage = new FormattedOutboundChatMessage();
                    formattedOutboundChatMessage.AppendPlainText(textMessage.Text);
                    // TODO parse URLs etc
                    _chatRoomSession.EndSendChatMessage(_chatRoomSession.BeginSendChatMessage(formattedOutboundChatMessage, null, null));
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e);
            }
            finally
            {
                _clientWebSocket.Dispose();
            }
        }

        private async void SessionChatMessageReceivedAsync(object sender, ChatMessageReceivedEventArgs e)
        {
            Console.WriteLine("\tChat message received");


            if (e.Message.FormattedMessageParts.Count == 0)
            {
                return;
            }
            if (e.Message.FormattedMessageParts[0].RawText.Substring(0, 7).Equals(_botName))
            {
                Console.WriteLine($"\t from:[{e.Message.MessageAuthor}]");
                Console.WriteLine($"\t room:[{e.Message.ChatRoomName}]");
                Console.WriteLine($"\t body:[{e.Message.MessageContent}]");
                Console.WriteLine($"\t parts:[{e.Message.FormattedMessageParts.Count}]");
                foreach (var part in e.Message.FormattedMessageParts)
                {
                    Console.WriteLine($"\t part:[{part.RawText}]");
                }
                await SendToHubotAsync(e.Message);
            }
        }

        private async Task SendToHubotAsync(ChatMessage message)
        {
            var textMessage = new TextMessage("text", message.MessageId, message.MessageAuthor.ToString(),
                message.ChatRoomName, message.MessageContent);
            var stringMessage = JsonConvert.SerializeObject(textMessage);
            Console.Out.WriteLine("Sending:" + stringMessage);
            var sendBytes = Encoding.UTF8.GetBytes(stringMessage);
            var sendBuffer = new ArraySegment<byte>(sendBytes);
            await _clientWebSocket.SendAsync(
                sendBuffer,
                WebSocketMessageType.Text, true, _cancellationToken);
        }
    }
}
