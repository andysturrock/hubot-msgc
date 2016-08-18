using System;
using System.Diagnostics;
using System.Net;
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

            var roomSnapshot = FindChatRoom();
            _chatRoomSession = JoinChatRoom(roomSnapshot);

            _chatRoomSession.ChatMessageReceived += SessionChatMessageReceived;
        }

        public void Send(string message)
        {
            // TODO parse URLs etc
            var formattedOutboundChatMessage = new FormattedOutboundChatMessage();
            formattedOutboundChatMessage.AppendPlainText(message);

            _chatRoomSession.EndSendChatMessage(
                _chatRoomSession.BeginSendChatMessage(formattedOutboundChatMessage, null, null));
        }

        public void Disconnect()
        {
            _chatRoomSession.EndLeave(_chatRoomSession.BeginLeave(null, null));
            _chatRoomSession.ChatMessageReceived -= SessionChatMessageReceived;

            DisconnectGroupChatServer();
            DisconnectOfficeCommunicationServer();
        }

        public event EventHandler<TextMessageReceivedEventArgs> TextMessageReceived;

        private void OnTextMessageReceived(TextMessageReceivedEventArgs e)
        {
            TextMessageReceived?.Invoke(this, e);
        }

        private void SessionChatMessageReceived(object sender, ChatMessageReceivedEventArgs e)
        {
            var textMessage = new TextMessage("text", e.Message.MessageId, e.Message.MessageAuthor.ToString(),
                e.Message.ChatRoomName, e.Message.MessageContent);
            OnTextMessageReceived(new TextMessageReceivedEventArgs(textMessage));
        }

        private UserEndpoint ConnectOfficeCommunicationServer()
        {
            // Create the OCS UserEndpoint and attempt to connect to OCS
            _eventLog.WriteEntry($"Connecting to OCS... [{_ocsServer}]");

            // Use the appropriate SipTransportType depending on current OCS deployment
            var platformSettings = new ClientPlatformSettings("GroupChat.Test", SipTransportType.Tls);
            var collabPlatform = new CollaborationPlatform(platformSettings);

            // Initialize the platform
            collabPlatform.EndStartup(collabPlatform.BeginStartup(null, null));

            // You can also pass in the server's port # here.
            var userEndpointSettings = new UserEndpointSettings(_userSipUri.ToString(), _ocsServer);

            // When usingSso is true use the current users credentials, otherwise use username and password
            userEndpointSettings.Credential = new NetworkCredential(_ocsUsername, _ocsPassword);

            var userEndpoint = new UserEndpoint(collabPlatform, userEndpointSettings);

            // Login to OCS.
            userEndpoint.EndEstablish(userEndpoint.BeginEstablish(null, null));

            _eventLog.WriteEntry("Success");
            return userEndpoint;
        }

        private GroupChatEndpoint ConnectGroupChatServer()
        {
            _eventLog.WriteEntry("Connecting to Group Chat Server...");

            GroupChatEndpoint groupChatEndpoint = new GroupChatEndpoint(_lookupServerUri, _userEndpoint);

            groupChatEndpoint.EndEstablish(groupChatEndpoint.BeginEstablish(null, null));

            _eventLog.WriteEntry("Success");
            return groupChatEndpoint;
        }

        private ChatRoomSnapshot FindChatRoom()
        {
            _eventLog.WriteEntry($"Searching for chat room [{_chatRoomName}]...");

            var chatServices = _groupChatEndpoint.GroupChatServices;

            var chatRooms = chatServices.EndBrowseChatRoomsByCriteria(
                chatServices.BeginBrowseChatRoomsByCriteria(_chatRoomName, false, null, null));

            _eventLog.WriteEntry($"Found {chatRooms.Count} chat room(s):");
            if (chatRooms.Count > 0)
            {
                foreach (var snapshot in chatRooms)
                    _eventLog.WriteEntry($"name: {snapshot.Name}\nuri:{snapshot.ChatRoomUri}");
                return chatRooms[0];
            }
            return null;
        }

        private ChatRoomSession JoinChatRoom(ChatRoomSummary summary)
        {
            _eventLog.WriteEntry($"Joining chat room [{summary.Name}]...");

            var session = new ChatRoomSession(_groupChatEndpoint);
            session.EndJoin(session.BeginJoin(summary, null, null));

            _eventLog.WriteEntry("Success");

            return session;
        }

        private void DisconnectGroupChatServer()
        {
            _eventLog.WriteEntry("Disconnecting from Group Chat Server...");

            _groupChatEndpoint.EndTerminate(_groupChatEndpoint.BeginTerminate(null, null));

            _eventLog.WriteEntry("Success");
        }

        private void DisconnectOfficeCommunicationServer()
        {
            _eventLog.WriteEntry("Disconnecting from OCS...");

            _userEndpoint.EndTerminate(_userEndpoint.BeginTerminate(null, null));

            CollaborationPlatform platform = _userEndpoint.Platform;
            platform.EndShutdown(platform.BeginShutdown(null, null));

            _eventLog.WriteEntry("Success");
        }
    }
}
