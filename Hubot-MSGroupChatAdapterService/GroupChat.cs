using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.GroupChat;
using Microsoft.Rtc.Signaling;

namespace Hubot_MSGroupChatAdapterService
{
    class GroupChat
    {
        private ChatRoomSession _chatRoomSession;
        private GroupChatEndpoint _groupChatEndpoint;
        private UserEndpoint _userEndpoint;
        private readonly Uri _userSipUri;
        private readonly string _ocsServer;
        private readonly string _ocsUsername;
        private readonly string _ocsPassword;
        private readonly Uri _lookupServerUri;
        // TODO room should be dynamic - ie a parameter for methods like Send etc.
        // Cache room session in Dictionary mapping room name to session?
        // Would also need to support event handler per room
        private readonly string _chatRoomName;
        private readonly EventLog _eventLog;

        public GroupChat(EventLog eventLog, Uri userSipUri, string ocsServer, string ocsUsername, string ocsPassword, Uri lookupServerUri, string chatRoomName)
        {
            _eventLog = eventLog;
            _userSipUri = userSipUri;
            _ocsServer = ocsServer;
            _ocsUsername = ocsUsername;
            _ocsPassword = ocsPassword;
            _lookupServerUri = lookupServerUri;
            _chatRoomName = chatRoomName;
        }

        public void Connect()
        {
            _userEndpoint = ConnectOfficeCommunicationServer();

            _groupChatEndpoint = ConnectGroupChatServer();

            ChatRoomSnapshot roomSnapshot = FindChatRoom();
            _chatRoomSession = JoinChatRoom(roomSnapshot);

            _chatRoomSession.ChatMessageReceived += SessionChatMessageReceivedAsync;
        }

        public async Task SendAsync(string message)
        {
            // TODO parse URLs etc
            var formattedOutboundChatMessage = new FormattedOutboundChatMessage();
            formattedOutboundChatMessage.AppendPlainText(message);

            await Task.Run(() => _chatRoomSession.EndSendChatMessage(
                _chatRoomSession.BeginSendChatMessage(formattedOutboundChatMessage, null, null)));
        }

        public void Disconnect()
        {
            _chatRoomSession.EndLeave(_chatRoomSession.BeginLeave(null, null));
            _chatRoomSession.ChatMessageReceived -= SessionChatMessageReceivedAsync;

            DisconnectGroupChatServer();
            DisconnectOfficeCommunicationServer();
        }

        public event EventHandler<TextMessageReceivedEventArgs> TextMessageReceived;

        private async Task OnTextMessageReceivedAsync(TextMessageReceivedEventArgs e)
        {
            await Task.Run(() => TextMessageReceived?.Invoke(this, e));
        }

        private async void SessionChatMessageReceivedAsync(object sender, ChatMessageReceivedEventArgs e)
        {
            var textMessage = new TextMessage("text", e.Message.MessageId, e.Message.MessageAuthor.ToString(),
                e.Message.ChatRoomName, e.Message.MessageContent);
            await OnTextMessageReceivedAsync(new TextMessageReceivedEventArgs(textMessage));
        }

        private UserEndpoint ConnectOfficeCommunicationServer()
        {
            // Create the OCS UserEndpoint and attempt to connect to OCS
            Console.WriteLine("Connecting to OCS... [{0}]", _ocsServer);

            // Use the appropriate SipTransportType depending on current OCS deployment
            ClientPlatformSettings platformSettings = new ClientPlatformSettings("GroupChat.Test", SipTransportType.Tls);
            CollaborationPlatform collabPlatform = new CollaborationPlatform(platformSettings);

            // Initialize the platform
            collabPlatform.EndStartup(collabPlatform.BeginStartup(null, null));

            // You can also pass in the server's port # here.
            UserEndpointSettings userEndpointSettings = new UserEndpointSettings(_userSipUri.ToString(), _ocsServer);

            // When usingSso is true use the current users credentials, otherwise use username and password
            userEndpointSettings.Credential = new NetworkCredential(_ocsUsername, _ocsPassword);

            UserEndpoint userEndpoint = new UserEndpoint(collabPlatform, userEndpointSettings);

            // Login to OCS.
            userEndpoint.EndEstablish(userEndpoint.BeginEstablish(null, null));

            Console.WriteLine("\tSuccess");
            return userEndpoint;
        }

        private GroupChatEndpoint ConnectGroupChatServer()
        {
            Console.WriteLine("Connecting to Group Chat Server...");

            GroupChatEndpoint groupChatEndpoint = new GroupChatEndpoint(_lookupServerUri, _userEndpoint);

            groupChatEndpoint.EndEstablish(groupChatEndpoint.BeginEstablish(null, null));

            Console.WriteLine("\tSuccess");
            return groupChatEndpoint;
        }

        private ChatRoomSnapshot FindChatRoom()
        {
            Console.WriteLine(String.Format("Searching for chat room [{0}]...", _chatRoomName));

            GroupChatServices chatServices = _groupChatEndpoint.GroupChatServices;

            ReadOnlyCollection<ChatRoomSnapshot> chatRooms = chatServices.EndBrowseChatRoomsByCriteria(
                chatServices.BeginBrowseChatRoomsByCriteria(_chatRoomName, false, null, null));

            Console.WriteLine(String.Format("\tFound {0} chat room(s):", chatRooms.Count));
            if (chatRooms.Count > 0)
            {
                foreach (ChatRoomSnapshot snapshot in chatRooms)
                    Console.WriteLine(String.Format("\tname: {0}\n\turi:{1}", snapshot.Name, snapshot.ChatRoomUri));
                return chatRooms[0];
            }
            return null;
        }

        private ChatRoomSession JoinChatRoom(ChatRoomSummary summary)
        {
            Console.WriteLine(String.Format("Joining chat room by NAME [{0}]...", summary.Name));

            ChatRoomSession session = new ChatRoomSession(_groupChatEndpoint);
            session.EndJoin(session.BeginJoin(summary, null, null));

            Console.WriteLine("\tSuccess");

            return session;
        }

        private void DisconnectGroupChatServer()
        {
            Console.WriteLine("Disconnecting from Group Chat Server...");

            _groupChatEndpoint.EndTerminate(_groupChatEndpoint.BeginTerminate(null, null));

            Console.WriteLine("\tSuccess");
        }

        private void DisconnectOfficeCommunicationServer()
        {
            Console.WriteLine("Disconnecting from OCS...");

            _userEndpoint.EndTerminate(_userEndpoint.BeginTerminate(null, null));

            CollaborationPlatform platform = _userEndpoint.Platform;
            platform.EndShutdown(platform.BeginShutdown(null, null));

            Console.WriteLine("\tSuccess");
        }
    }
}
