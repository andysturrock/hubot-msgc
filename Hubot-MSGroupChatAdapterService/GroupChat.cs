using System;
using System.Net;
using System.Timers;
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

        private readonly Timer _timer = new Timer();

        public bool Connected { get; private set; }

        public GroupChat(Uri userSipUri, string ocsServer, string ocsUsername, string ocsPassword, Uri lookupServerUri,
            string chatRoomName) : this(userSipUri, ocsServer, lookupServerUri, chatRoomName)
        {
            _ocsUsername = ocsUsername;
            _ocsPassword = ocsPassword;
            _useSso = false;
        }

        public GroupChat(Uri userSipUri, string ocsServer, Uri lookupServerUri, string chatRoomName)
        {
            _userSipUri = userSipUri;
            _ocsServer = ocsServer;
            _lookupServerUri = lookupServerUri;
            _chatRoomName = chatRoomName;
            _useSso = true;
            Connected = false;

            // Spin up a thread to check the state every few seconds...
            _timer.Elapsed += OnTimerEvent;
            _timer.Interval = 5000;
        }

        public void Connect()
        {
            _userEndpoint = ConnectOfficeCommunicationServer();
            _userEndpoint.StateChanged += StateChanged;

            _groupChatEndpoint = ConnectGroupChatServer();
            _groupChatEndpoint.ConnectionStateChanged += ConnectionStateChanged;

            var roomSnapshot = FindChatRoom();
            _chatRoomSession = JoinChatRoom(roomSnapshot);
            _chatRoomSession.ChatMessageReceived += ChatMessageReceived;
            _chatRoomSession.ChatRoomSessionStateChanged += ChatRoomSessionStateChanged;

            // Start checking session status
            _timer.Enabled = true;

            Connected = true;
        }

        // Implement a call with the right signature for events going off
        private void OnTimerEvent(object source, ElapsedEventArgs e)
        {
            if (_userEndpoint.State != LocalEndpointState.Established ||
                _groupChatEndpoint.State != GroupChatEndpointState.Established ||
                _chatRoomSession.State != ChatRoomSessionState.Established)
            {
                Disconnect("Server disconnected");
            }
        }

        private void StateChanged(object sender, LocalEndpointStateChangedEventArgs e)
        {
            if (e.State == LocalEndpointState.Terminated || e.State == LocalEndpointState.Terminated)
            {
                Disconnect("Server disconnected");
            }
        }

        private void ChatRoomSessionStateChanged(object sender, ChatRoomSessionStateChangedEventArgs e)
        {
            if (e.State == ChatRoomSessionState.Terminating || e.State == ChatRoomSessionState.Terminated)
            {
                Disconnect("Server disconnected");
            }
        }

        private void ConnectionStateChanged(object sender, GroupChatEndpointStateChangedEventArgs e)
        {
            if (e.State == GroupChatEndpointState.Terminating || e.State == GroupChatEndpointState.Terminated)
            {
                Disconnect("Server disconnected");
            }
        }

        public void Send(string message)
        {
            // TODO parse URLs etc
            var formattedOutboundChatMessage = new FormattedOutboundChatMessage();
            formattedOutboundChatMessage.AppendPlainText(message);

            try
            {
                _chatRoomSession.EndSendChatMessage(
                    _chatRoomSession.BeginSendChatMessage(formattedOutboundChatMessage, null, null));
            }
            catch (Exception e)
            {
                Disconnect("Server disconnected: " + e);
            }
        }

        public void Disconnect()
        {
            Disconnect("Client requested disconnect");
        }

        private void Disconnect(string reason)
        {
            // Stop checking session status
            _timer.Enabled = false;
            // And remove all our event listeners otherwise we'll end up back here in infinite recursion
            _userEndpoint.StateChanged -= StateChanged;
            _groupChatEndpoint.ConnectionStateChanged -= ConnectionStateChanged;
            _chatRoomSession.ChatRoomSessionStateChanged -= ChatRoomSessionStateChanged;
            _chatRoomSession.ChatMessageReceived -= ChatMessageReceived;
            try
            {
                _chatRoomSession.EndLeave(_chatRoomSession.BeginLeave(null, null));
            }
            catch (Exception)
            {
                // Ignore and continue
            }
            try
            {
                DisconnectGroupChatServer();
            }
            catch (Exception)
            {
                // Ignore and continue
            }
            try
            {
                DisconnectOfficeCommunicationServer();
            }
            catch (Exception)
            {
                // Ignore and continue
            }

            Connected = false;
            OnDisconnected(new DisconnectedEventArgs(reason));
        }

        public event EventHandler<TextMessageReceivedEventArgs> TextMessageReceived;

        private void OnTextMessageReceived(TextMessageReceivedEventArgs e)
        {
            TextMessageReceived?.Invoke(this, e);
        }

        public event EventHandler<DisconnectedEventArgs> Disconnected;

        private void OnDisconnected(DisconnectedEventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }

        private void ChatMessageReceived(object sender, ChatMessageReceivedEventArgs e)
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
            userEndpointSettings.Credential = _useSso
                ? SipCredentialCache.DefaultCredential
                : new NetworkCredential(_ocsUsername, _ocsPassword);
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

            var platform = _userEndpoint.Platform;
            platform.EndShutdown(platform.BeginShutdown(null, null));
        }
    }
}
