using System;
using System.Net;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.GroupChat;
using Microsoft.Rtc.Signaling;

namespace Hubot_MSGroupChatAdapterService
{
    public class GroupChat
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
        private readonly bool _useSso;

        public GroupChat(Uri userSipUri, string ocsServer, string ocsUsername, string ocsPassword, Uri lookupServerUri, string chatRoomName)
        {
            _userSipUri = userSipUri;
            _ocsServer = ocsServer;
            _ocsUsername = ocsUsername;
            _ocsPassword = ocsPassword;
            _lookupServerUri = lookupServerUri;
            _chatRoomName = chatRoomName;
            _useSso = false;
        }

        public GroupChat(Uri userSipUri, string ocsServer, Uri lookupServerUri, string chatRoomName)
        {
            _userSipUri = userSipUri;
            _ocsServer = ocsServer;
            _lookupServerUri = lookupServerUri;
            _chatRoomName = chatRoomName;
            _useSso = true;
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
            var clientPlatformSettings = new ClientPlatformSettings("GroupChat.Test", SipTransportType.Tls);
            var collaborationPlatform = new CollaborationPlatform(clientPlatformSettings);

            collaborationPlatform.EndStartup(collaborationPlatform.BeginStartup(null, null));

            var userEndpointSettings = new UserEndpointSettings(_userSipUri.ToString(), _ocsServer);
            userEndpointSettings.Credential = _useSso ? SipCredentialCache.DefaultCredential : new NetworkCredential(_ocsUsername, _ocsPassword);
            var userEndpoint = new UserEndpoint(collaborationPlatform, userEndpointSettings);

            userEndpoint.EndEstablish(userEndpoint.BeginEstablish(null, null));

            return userEndpoint;
        }

        private GroupChatEndpoint ConnectGroupChatServer()
        {
            var groupChatEndpoint = new GroupChatEndpoint(_lookupServerUri, _userEndpoint);

            groupChatEndpoint.EndEstablish(groupChatEndpoint.BeginEstablish(null, null));

            return groupChatEndpoint;
        }

        private ChatRoomSnapshot FindChatRoom()
        {
            var chatServices = _groupChatEndpoint.GroupChatServices;

            var chatRooms = chatServices.EndBrowseChatRoomsByCriteria(
                chatServices.BeginBrowseChatRoomsByCriteria(_chatRoomName, false, null, null));

            if (chatRooms.Count > 0)
            {
                return chatRooms[0];
            }
            return null;
        }

        private ChatRoomSession JoinChatRoom(ChatRoomSummary summary)
        {
            var session = new ChatRoomSession(_groupChatEndpoint);
            session.EndJoin(session.BeginJoin(summary, null, null));

            return session;
        }

        private void DisconnectGroupChatServer()
        {
            _groupChatEndpoint.EndTerminate(_groupChatEndpoint.BeginTerminate(null, null));
        }

        private void DisconnectOfficeCommunicationServer()
        {
            _userEndpoint.EndTerminate(_userEndpoint.BeginTerminate(null, null));

            CollaborationPlatform platform = _userEndpoint.Platform;
            platform.EndShutdown(platform.BeginShutdown(null, null));
        }
    }
}
